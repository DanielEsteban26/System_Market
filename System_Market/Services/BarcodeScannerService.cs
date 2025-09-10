using System;
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
        private static DateTime _lastCharTime = DateTime.MinValue;
        private static readonly System.Collections.Generic.List<double> _intervalsMs = new();
        private static DispatcherTimer? _idleTimer;

        // Umbrales típicos para scanner (ajustables)
        private const int MinLength = 5;          // Largo mínimo de código válido
        private const int MaxAvgIntervalMs = 35;  // Promedio de intervalo entre teclas para considerarlo “rápido”
        private const int IdleFinalizeMs = 120;   // Inactividad para finalizar y evaluar

        public static void Start()
        {
            if (_started) return;
            _started = true;

            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleFinalizeMs) };
            _idleTimer.Tick += (_, __) =>
            {
                _idleTimer!.Stop();
                TryFinalize(); // Finaliza por inactividad
                Reset();
            };

            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        private static void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
        {
            // Captura caracteres
            if (e.StagingItem.Input is TextCompositionEventArgs tc && !string.IsNullOrEmpty(tc.Text))
            {
                var now = DateTime.UtcNow;
                if (_lastCharTime != DateTime.MinValue)
                {
                    _intervalsMs.Add((now - _lastCharTime).TotalMilliseconds);
                }
                _lastCharTime = now;

                _buffer.Append(tc.Text);

                _idleTimer!.Stop();
                _idleTimer.Start();
                return;
            }

            // Finaliza en Enter o Tab (muchos scanners envían Enter/Tab)
            if (e.StagingItem.Input is KeyEventArgs ke &&
                ke.RoutedEvent == Keyboard.KeyDownEvent &&
                (ke.Key == Key.Return || ke.Key == Key.Enter || ke.Key == Key.Tab))
            {
                _idleTimer!.Stop();
                TryFinalize();
                Reset();
            }
        }

        private static void TryFinalize()
        {
            var code = _buffer.ToString().Trim();
            if (code.Length < MinLength) return;
            if (_intervalsMs.Count == 0) return;

            var avg = _intervalsMs.Average();
            if (avg <= MaxAvgIntervalMs)
            {
                // Detección positiva: abrir diálogo en UI
                Application.Current.Dispatcher.Invoke(() =>
                {
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
            _lastCharTime = DateTime.MinValue;
        }
    }
}