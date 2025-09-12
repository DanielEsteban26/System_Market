using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System_Market.Views;

namespace System_Market.Services
{
    public static class BarcodeScannerService
    {
        private static bool _started;

        private static readonly StringBuilder _buffer = new();
        private static readonly List<double> _intervalsMs = new();
        private static DateTime _lastKeyTime = DateTime.MinValue;
        private static DispatcherTimer? _idleTimer;

        // Parámetros ajustables
        private const int MinLength = 5;
        private const int ExpectedLength = 13;          // EAN-13 (solo como optimización; no bloquea alfanuméricos)
        private const int IdleFinalizeMs = 140;         // ligera holgura
        private const int MaxAvgIntervalMs = 80;        // permitir lectores un poco más lentos
        private const int MaxLength = 256;

        private static readonly int[] PlausibleLengths = { 8, 12, 13, 14, 18 }; // heurística opcional

        private const bool EnableDebug = false;

        public static void Start()
        {
            if (_started) return;
            _started = true;

            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleFinalizeMs) };
            _idleTimer.Tick += (_, __) =>
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
            };

            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        private static void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
        {
            // Finalización por Enter/Tab
            if (e.StagingItem.Input is KeyEventArgs ke &&
                ke.RoutedEvent == Keyboard.KeyDownEvent &&
                (ke.Key == Key.Return || ke.Key == Key.Enter || ke.Key == Key.Tab))
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
                return;
            }

            // Tomar caracteres de KeyDown (mapeo extendido)
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

                if (_buffer.Length > MaxLength)
                {
                    if (EnableDebug) Debug.WriteLine($"[Scanner] Exceso longitud ({_buffer.Length}), forzando finalización.");
                    _idleTimer!.Stop();
                    TryFinalize();
                    Reset();
                    return;
                }

                if (_buffer.Length == ExpectedLength && _intervalsMs.Count > 0 && _intervalsMs.Average() <= MaxAvgIntervalMs)
                {
                    _idleTimer!.Stop();
                    TryFinalize();
                    Reset();
                    return;
                }

                _idleTimer!.Stop();
                _idleTimer.Start();
            }
        }

        private static void TryFinalize()
        {
            if (_buffer.Length < MinLength) return;
            if (_intervalsMs.Count == 0) return;

            var avg = _intervalsMs.Average();
            if (avg <= MaxAvgIntervalMs)
            {
                var code = _buffer.ToString();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Venta activa
                    var ventaActiva = Application.Current.Windows.OfType<VentaWindow>().FirstOrDefault(w => w.IsActive);
                    if (ventaActiva != null)
                    {
                        ventaActiva.HandleScannedCode(code);
                        return;
                    }

                    // Compra activa
                    var compraActiva = Application.Current.Windows.OfType<CompraWindow>().FirstOrDefault(w => w.IsActive);
                    if (compraActiva != null)
                    {
                        compraActiva.HandleScannedCode(code);
                        return;
                    }

                    // Ventana de opciones (si aplica)
                    if (Application.Current.Windows.OfType<ScanOptionsWindow>().Any())
                        return;

                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                ?? Application.Current.MainWindow;
                    var dlg = new ScanOptionsWindow(code)
                    {
                        Owner = owner,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Topmost = true
                    };
                    dlg.ShowDialog();
                });
            }
        }

        private static void Reset()
        {
            _buffer.Clear();
            _intervalsMs.Clear();
            _lastKeyTime = DateTime.MinValue;
        }

        private static bool IsShiftPressed() =>
            Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // Mapea teclas a caracteres (alfanuméricos + símbolos comunes en códigos de barras)
        private static char? MapKeyToChar(Key key, bool shift, bool caps)
        {
            // Dígitos fila superior (sin Shift) y numpad
            if (key >= Key.D0 && key <= Key.D9 && !shift)
                return (char)('0' + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (char)('0' + (key - Key.NumPad0));

            // Letras A-Z (respetando Shift y CapsLock)
            if (key >= Key.A && key <= Key.Z)
            {
                char c = (char)('a' + (key - Key.A));
                bool upper = shift ^ caps; // XOR: Shift o Caps activan mayúscula
                return upper ? char.ToUpperInvariant(c) : c;
            }

            // Símbolos comunes por layout en-US (suficiente para la mayoría de scanners en modo teclado)
            return key switch
            {
                Key.OemMinus => shift ? '_' : '-',
                Key.Subtract => '-', // Numpad
                Key.OemPlus => shift ? '+' : '+',
                Key.Add => '+',      // Numpad
                Key.Multiply => '*', // Numpad
                Key.Divide => '/',   // Numpad
                Key.Oem2 => shift ? '?' : '/',      // / ?
                Key.OemPeriod => '.',               // .
                Key.OemComma => ',',                // ,
                Key.Oem1 => shift ? ':' : ';',      // ; :
                Key.Oem3 => shift ? '~' : '`',      // ` ~
                Key.Oem4 => shift ? '{' : '[',      // [ {
                Key.Oem5 => shift ? '|' : '\\',     // \ |
                Key.Oem6 => shift ? '}' : ']',      // ] }
                Key.Oem7 => shift ? '"' : '\'',     // ' "
                Key.Space => ' ',
                _ => null
            };
        }
    }
}