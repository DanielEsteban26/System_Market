using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System_Market.Views;
using System.Windows.Controls; // añadido
using System.Windows.Controls.Primitives; // añadido

namespace System_Market.Services
{
    // Servicio estático que detecta y procesa la entrada de códigos de barras desde un lector (emulando teclado).
    // Permite que cualquier ventana del sistema reciba notificaciones cuando se escanea un código.
    public static class BarcodeScannerService
    {
        // Indica si el servicio ya fue iniciado para evitar múltiples suscripciones.
        private static bool _started;

        // Buffer para acumular los caracteres recibidos del escáner.
        private static readonly StringBuilder _buffer = new();
        // Lista de intervalos de tiempo (en ms) entre cada tecla, para distinguir entre escaneo y tipeo manual.
        private static readonly List<double> _intervalsMs = new();
        // Marca de tiempo de la última tecla recibida.
        private static DateTime _lastKeyTime = DateTime.MinValue;
        // Timer para detectar inactividad y finalizar el escaneo.
        private static DispatcherTimer? _idleTimer;

        // Parámetros ajustables para la lógica de escaneo.
        private const int MinLength = 5;              // Longitud mínima para considerar un código válido.
        private const int ExpectedLength = 13;        // Longitud típica de un código de barras EAN-13.
        private const int IdleFinalizeMs = 140;       // Tiempo de inactividad para finalizar el escaneo.
        private const int MaxAvgIntervalMs = 80;      // Máximo promedio de intervalo para considerar "escaneo rápido".
        private const int MaxLength = 256;            // Longitud máxima aceptada para un código.
        private static readonly int[] PlausibleLengths = { 8, 12, 13, 14, 18 }; // Longitudes típicas de códigos.

        private const bool EnableDebug = false;       // Habilita mensajes de depuración.

        // Guarda el último código escaneado y su timestamp, útil para reintentos.
        private static string? _lastCode;
        private static DateTime _lastCodeTime;

        // Evento que se dispara cuando se detecta un código de barras (puede ser usado por cualquier ventana).
        public static event Action<string>? CodeScanned;

        // Evento público alternativo para suscribirse a la notificación de escaneo.
        public static event Action<string>? OnCodeScanned
        {
            add { CodeScanned += value; }
            remove { CodeScanned -= value; }
        }

        // Cola de códigos pendientes para ventas, útil si no hay ventana lista para procesar el escaneo.
        private static readonly Queue<string> _pendingVentaCodes = new();

        // Inicia el servicio de escaneo, suscribiéndose a los eventos de entrada de teclado globales.
        public static void Start()
        {
            if (_started) return;
            _started = true;

            // Timer para detectar el fin de un escaneo por inactividad.
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleFinalizeMs) };
            _idleTimer.Tick += (_, __) =>
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
            };

            // Se suscribe al evento global de entrada de teclado antes de que llegue a los controles.
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        // Procesa cada evento de teclado antes de que llegue a los controles.
        private static void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
        {
            // Si se presiona Enter, Return o Tab, se asume que terminó el escaneo y se procesa el buffer.
            if (e.StagingItem.Input is KeyEventArgs ke &&
                ke.RoutedEvent == Keyboard.KeyDownEvent &&
                (ke.Key == Key.Return || ke.Key == Key.Enter || ke.Key == Key.Tab))
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
                return;
            }

            // Si es una tecla válida, la convierte a carácter y la agrega al buffer.
            if (e.StagingItem.Input is KeyEventArgs keyArgs &&
                keyArgs.RoutedEvent == Keyboard.KeyDownEvent)
            {
                var ch = MapKeyToChar(keyArgs.Key, IsShiftPressed(), Keyboard.IsKeyToggled(Key.CapsLock));
                if (ch == null) return;

                var now = DateTime.UtcNow;
                if (_lastKeyTime != DateTime.MinValue)
                    _intervalsMs.Add((now - _lastKeyTime).TotalMilliseconds);
                _lastKeyTime = now;

                _buffer.Append(ch.Value);

                // Si el buffer supera la longitud máxima, se descarta y se reinicia.
                if (_buffer.Length > MaxLength)
                {
                    if (EnableDebug) Debug.WriteLine($"[Scanner] Exceso longitud {_buffer.Length}");
                    _idleTimer!.Stop();
                    TryFinalize();
                    Reset();
                    return;
                }

                // Si se alcanza la longitud esperada y la velocidad es de escaneo, se procesa automáticamente.
                if (_buffer.Length == ExpectedLength && _intervalsMs.Count > 0 && _intervalsMs.Average() <= MaxAvgIntervalMs)
                {
                    _idleTimer!.Stop();
                    TryFinalize();
                    Reset();
                    return;
                }

                // Reinicia el timer de inactividad para seguir acumulando caracteres.
                _idleTimer!.Stop();
                _idleTimer.Start();
            }
        }

        // Determina si un código es plausible (por longitud o por ser alfanumérico).
        private static bool EsCodigoPlausible(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            if (code.Length < MinLength || code.Length > 64) return false;
            if (PlausibleLengths.Contains(code.Length)) return true;
            // Acepta si todos los caracteres son dígitos o letras.
            return code.All(char.IsLetterOrDigit);
        }

        // Intenta finalizar el escaneo y notificar a la ventana correspondiente.
        private static void TryFinalize()
        {
            if (_buffer.Length < MinLength) return;

            var code = _buffer.ToString();
            var avg = _intervalsMs.Count == 0 ? 0 : _intervalsMs.Average();
            bool fast = avg <= MaxAvgIntervalMs;

            // Si no es un escaneo rápido y tampoco es plausible, se descarta.
            if (!fast && !EsCodigoPlausible(code))
                return;

            // Si el foco está en un control editable, se asume que el usuario está escribiendo manualmente.
            try
            {
                var focused = Keyboard.FocusedElement;
                if (focused is TextBoxBase || focused is TextBox || focused is PasswordBox)
                {
                    if (EnableDebug) Debug.WriteLine("[Scanner] Ignorado porque el foco está en un control editable.");
                    return;
                }
            }
            catch
            {
                // Si ocurre un error al obtener el foco, se ignora y se continúa.
            }

            // Guarda el último código y su timestamp.
            _lastCode = code;
            _lastCodeTime = DateTime.UtcNow;

            // Busca la ventana adecuada para manejar el código escaneado, en orden de prioridad.
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool handled = false;

                // 1) Si hay una ventana de venta activa, le pasa el código.
                var ventaWin = Application.Current.Windows
                    .OfType<VentaWindow>()
                    .Where(w => w.IsVisible)
                    .OrderByDescending(w => w.IsActive)
                    .ThenByDescending(w => w.IsFocused)
                    .FirstOrDefault();
                if (ventaWin != null)
                {
                    ventaWin.HandleScannedCode(code);
                    handled = true;
                }

                // 2) Si no, busca una ventana de compra activa.
                if (!handled)
                {
                    var compraWin = Application.Current.Windows
                        .OfType<CompraWindow>()
                        .Where(w => w.IsVisible)
                        .OrderByDescending(w => w.IsActive)
                        .ThenByDescending(w => w.IsFocused)
                        .FirstOrDefault();
                    if (compraWin != null)
                    {
                        compraWin.HandleScannedCode(code);
                        handled = true;
                    }
                }

                // 3) Si no, busca una ventana de edición de producto activa.
                if (!handled)
                {
                    var prodEditWin = Application.Current.Windows
                        .OfType<ProductoEdicionWindow>()
                        .Where(w => w.IsVisible)
                        .OrderByDescending(w => w.IsActive)
                        .ThenByDescending(w => w.IsFocused)
                        .FirstOrDefault();
                    if (prodEditWin != null)
                    {
                        prodEditWin.HandleScannedCode(code);
                        handled = true;
                    }
                }

                // 4) Si no, busca una ventana de listado de productos activa.
                if (!handled)
                {
                    var prodWin = Application.Current.Windows
                        .OfType<ProductoWindow>()
                        .Where(w => w.IsVisible)
                        .OrderByDescending(w => w.IsActive)
                        .ThenByDescending(w => w.IsFocused)
                        .FirstOrDefault();
                    if (prodWin != null)
                    {
                        prodWin.HandleScannedCode(code);
                        handled = true;
                    }
                }

                // 5) Si ninguna ventana especializada está activa, lo pasa a la MainWindow.
                if (!handled)
                {
                    var mainWin = Application.Current.Windows
                        .OfType<System_Market.MainWindow>()
                        .FirstOrDefault(w => w.IsVisible);

                    if (mainWin != null)
                    {
                        mainWin.HandleScannedCode(code);
                        handled = true;
                    }
                    else
                    {
                        // Si no hay ventana lista, encola el código para procesarlo después.
                        if (_pendingVentaCodes.Count > 5) _pendingVentaCodes.Dequeue();
                        _pendingVentaCodes.Enqueue(code);
                    }
                }

                // Si ninguna ventana lo manejó, notifica a los suscriptores generales.
                if (!handled)
                {
                    CodeScanned?.Invoke(code);
                }

                if (EnableDebug)
                    Debug.WriteLine($"[Scanner] Code='{code}' handled={handled}");
            });
        }

        // Permite a una nueva VentaWindow volver a procesar el último código escaneado si es reciente.
        public static bool TryReplayLastCodeFor(VentaWindow window, TimeSpan maxAge)
        {
            if (_lastCode == null) return false;
            if ((DateTime.UtcNow - _lastCodeTime) > maxAge) return false;
            window.HandleScannedCode(_lastCode);
            return true;
        }

        // Devuelve y limpia la lista de códigos pendientes de venta.
        public static List<string> DrainPendingVentaCodes()
        {
            var list = new List<string>(_pendingVentaCodes);
            _pendingVentaCodes.Clear();
            return list;
        }

        // Limpia el buffer y los intervalos para iniciar un nuevo escaneo.
        private static void Reset()
        {
            _buffer.Clear();
            _intervalsMs.Clear();
            _lastKeyTime = DateTime.MinValue;
        }

        // Devuelve true si la tecla Shift está presionada.
        private static bool IsShiftPressed() =>
            Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // Convierte una tecla de tipo Key en un carácter, considerando Shift y CapsLock.
        private static char? MapKeyToChar(Key key, bool shift, bool caps)
        {
            if (key >= Key.D0 && key <= Key.D9 && !shift)
                return (char)('0' + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (char)('0' + (key - Key.NumPad0));

            if (key >= Key.A && key <= Key.Z)
            {
                char c = (char)('a' + (key - Key.A));
                bool upper = shift ^ caps;
                return upper ? char.ToUpperInvariant(c) : c;
            }

            // Mapea teclas especiales y de símbolos.
            return key switch
            {
                Key.OemMinus => shift ? '_' : '-',
                Key.Subtract => '-',
                Key.OemPlus => shift ? '+' : '+',
                Key.Add => '+',
                Key.Multiply => '*',
                Key.Divide => '/',
                Key.Oem2 => shift ? '?' : '/',
                Key.OemPeriod => '.',
                Key.OemComma => ',',
                Key.Oem1 => shift ? ':' : ';',
                Key.Oem3 => shift ? '~' : '`',
                Key.Oem4 => shift ? '{' : '[',
                Key.Oem5 => shift ? '|' : '\\',
                Key.Oem6 => shift ? '}' : ']',
                Key.Oem7 => shift ? '"' : '\'',
                Key.Space => ' ',
                _ => null
            };
        }
    }
}