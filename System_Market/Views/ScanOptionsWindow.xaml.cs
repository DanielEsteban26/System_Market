using System.Windows;
using System.Windows.Input;
using System_Market.Data;

namespace System_Market.Views
{
    public partial class ScanOptionsWindow : Window
    {
        private readonly string _codigo;
        public ScanOptionsWindow(string codigo)
        {
            InitializeComponent();
            _codigo = codigo;
            txtCodigo.Text = codigo;
        }

        private void AgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            // Copiamos el código al portapapeles para pegarlo en el formulario si aplica
            try { Clipboard.SetText(_codigo); } catch { }
            // Abrimos el editor de producto (pasamos conexión si el ctor lo requiere)
            var conn = DatabaseInitializer.GetConnectionString();
            var win = new ProductoEdicionWindow(conn);
            win.Show();
            Close();
        }

        private void NuevaCompra_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_codigo); } catch { }
            new CompraWindow().Show();
            Close();
        }

        private void NuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_codigo); } catch { }
            new VentaWindow().Show();
            Close();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // ESC para cerrar rápido
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            base.OnPreviewKeyDown(e);
        }
    }
}