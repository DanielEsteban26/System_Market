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
        private const int ExpectedLength = 13;
        private const int IdleFinalizeMs = 140;
        private const int MaxAvgIntervalMs = 80;
        private const int MaxLength = 256;
        private static readonly int[] PlausibleLengths = { 8, 12, 13, 14, 18 };

        private const bool EnableDebug = false;

        // NUEVO: cache del último código
        private static string? _lastCode;
        private static DateTime _lastCodeTime;

        // Evento opcional (si alguna vista quiere engancharse)
        public static event Action<string>? CodeScanned;

        private static readonly Queue<string> _pendingVentaCodes = new();   // cola simple (UI thread)

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
            if (e.StagingItem.Input is KeyEventArgs ke &&
                ke.RoutedEvent == Keyboard.KeyDownEvent &&
                (ke.Key == Key.Return || ke.Key == Key.Enter || ke.Key == Key.Tab))
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
                return;
            }

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
                    if (EnableDebug) Debug.WriteLine($"[Scanner] Exceso longitud {_buffer.Length}");
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

        private static bool EsCodigoPlausible(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            if (code.Length < MinLength || code.Length > 64) return false;
            if (PlausibleLengths.Contains(code.Length)) return true;
            // Aceptar si todos son dígitos o alfanumérico
            return code.All(char.IsLetterOrDigit);
        }

        private static void TryFinalize()
        {
            if (_buffer.Length < MinLength) return;

            var code = _buffer.ToString();
            var avg = _intervalsMs.Count == 0 ? 0 : _intervalsMs.Average();
            bool fast = avg <= MaxAvgIntervalMs;

            // Antes se descartaba si no era "fast"; ahora aceptamos si es plausible
            if (!fast && !EsCodigoPlausible(code))
                return;

            _lastCode = code;
            _lastCodeTime = DateTime.UtcNow;

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool handled = false;

                // 1) Venta (prioridad: evitar reabrir creación cuando hay VentaWindow abierta)
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

                // 2) Compra
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

                // 3) Edición de producto (si no fue manejado por venta/compra)
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

                // 4) Listado de productos
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

                // 5) Cola pendiente para futura Venta
                if (!handled)
                {
                    if (_pendingVentaCodes.Count > 5) _pendingVentaCodes.Dequeue();
                    _pendingVentaCodes.Enqueue(code);
                }

                CodeScanned?.Invoke(code);

                if (EnableDebug)
                    Debug.WriteLine($"[Scanner] Code='{code}' handled={handled}");
            });
        }

        // Permite a una nueva VentaWindow re-consumir el último código si fue escaneado hace muy poco
        public static bool TryReplayLastCodeFor(VentaWindow window, TimeSpan maxAge)
        {
            if (_lastCode == null) return false;
            if ((DateTime.UtcNow - _lastCodeTime) > maxAge) return false;
            window.HandleScannedCode(_lastCode);
            return true;
        }

        public static List<string> DrainPendingVentaCodes()
        {
            var list = new List<string>(_pendingVentaCodes);
            _pendingVentaCodes.Clear();
            return list;
        }

        private static void Reset()
        {
            _buffer.Clear();
            _intervalsMs.Clear();
            _lastKeyTime = DateTime.MinValue;
        }

        private static bool IsShiftPressed() =>
            Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

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