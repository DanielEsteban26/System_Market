using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class HistorialComprasWindow : Window
    {
        private readonly CompraService _compraService;

        public HistorialComprasWindow()
        {
            InitializeComponent();
            _compraService = new CompraService(DatabaseInitializer.GetConnectionString());
            CargarCompras();
        }

        private void CargarCompras()
        {
            dgCompras.ItemsSource = _compraService.ObtenerTodas();
            dgDetallesCompra.ItemsSource = null;
            btnAnularCompra.IsEnabled = false;
        }

        private void dgCompras_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var compra = dgCompras.SelectedItem as Compra;
            if (compra != null)
            {
                dgDetallesCompra.ItemsSource = _compraService.ObtenerDetallesPorCompra(compra.Id);
                btnAnularCompra.IsEnabled = compra.Estado == "Activa";
            }
            else
            {
                dgDetallesCompra.ItemsSource = null;
                btnAnularCompra.IsEnabled = false;
            }
        }

        private void btnAnularCompra_Click(object sender, RoutedEventArgs e)
        {
            var compra = dgCompras.SelectedItem as Compra;
            if (compra == null || compra.Estado != "Activa") return;

            if (MessageBox.Show(
                    "¿Seguro que desea anular esta compra?\n" +
                    "Se revertirá el stock de los productos y la compra quedará marcada como Anulada.",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var motivoWindow = new MotivoAnulacionWindow();
                if (motivoWindow.ShowDialog() == true)
                {
                    string motivo = motivoWindow.Motivo;
                    _compraService.AnularCompra(compra.Id, motivo);
                    MessageBox.Show("Compra anulada correctamente.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarCompras();
                }
            }
        }

        private void btnRefrescar_Click(object sender, RoutedEventArgs e) => CargarCompras();

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}