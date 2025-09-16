using System;
using System.Windows;
using System.Windows.Input;
using System_Market.Data;
using System_Market.Services;
using System_Market.Models;

namespace System_Market.Views
{
    public partial class ScanOptionsWindow : Window
    {
        private readonly string _codigo;
        private readonly ProductoService _productoService;
        private readonly string _conn;
        private readonly Producto? _productoEncontrado;
        private bool _navegando; // evita doble ejecución

        public ScanOptionsWindow(string codigo)
        {
            InitializeComponent();
            _codigo = codigo;
            txtCodigo.Text = codigo;

            _conn = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(_conn);
            _productoEncontrado = _productoService.ObtenerPorCodigoBarras(_codigo);

            ConfigurarOpciones();
        }

        private void ConfigurarOpciones()
        {
            bool existe = _productoEncontrado != null;
            if (btnNuevaVenta  != null) btnNuevaVenta.Visibility  = existe ? Visibility.Visible : Visibility.Collapsed;
            if (btnNuevaCompra != null) btnNuevaCompra.Visibility = existe ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Navegar(Action abrir)
        {
            if (_navegando) return;
            _navegando = true;

            // Oculta de inmediato para que no tape la nueva ventana
            Topmost = false;
            Hide();

            // Abre en el siguiente tick y luego cierra definitivamente este modal
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { abrir(); }
                finally { Close(); }
            }));
        }

        private void AgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            var codigo = _codigo;
            var conn = _conn;

            Navegar(() =>
            {
                var win = new ProductoEdicionWindow(conn, null, codigo, bloquearCodigo: true)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var ok = win.ShowDialog() == true;
                if (ok && win.Producto != null)
                {
                    try
                    {
                        _productoService.AgregarProducto(win.Producto);
                        MessageBox.Show("Producto registrado correctamente.", "Producto",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al guardar el producto: " + ex.Message,
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        private void NuevaCompra_Click(object sender, RoutedEventArgs e)
        {
            if (_productoEncontrado == null)
            {
                MessageBox.Show("El código no existe. Cree el producto primero.", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var codigo = _codigo;
            // Elimina el argumento 'true' ya que CompraWindow no tiene un constructor que lo acepte
            Navegar(() => new CompraWindow().Show());
        }

        private void NuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            if (_productoEncontrado == null)
            {
                MessageBox.Show("El código no existe. Cree el producto primero.", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var codigo = _codigo;
            Navegar(() => new VentaWindow(codigo).Show());
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}