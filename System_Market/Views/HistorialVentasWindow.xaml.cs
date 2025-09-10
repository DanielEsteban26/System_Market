using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class HistorialVentasWindow : Window
    {
        private readonly VentaService _ventaService;

        public HistorialVentasWindow()
        {
            InitializeComponent();
            _ventaService = new VentaService(DatabaseInitializer.GetConnectionString());
            CargarVentas();
        }

        private void CargarVentas()
        {
            dgVentas.ItemsSource = _ventaService.ObtenerTodas();
            dgDetallesVenta.ItemsSource = null;
            btnAnularVenta.IsEnabled = false;
        }

        private void dgVentas_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var venta = dgVentas.SelectedItem as Venta;
            if (venta != null)
            {
                dgDetallesVenta.ItemsSource = _ventaService.ObtenerDetallesPorVenta(venta.Id);
                btnAnularVenta.IsEnabled = venta.Estado == "Activa";
            }
            else
            {
                dgDetallesVenta.ItemsSource = null;
                btnAnularVenta.IsEnabled = false;
            }
        }

        private void btnAnularVenta_Click(object sender, RoutedEventArgs e)
        {
            var venta = dgVentas.SelectedItem as Venta;
            if (venta == null || venta.Estado != "Activa") return;

            if (MessageBox.Show(
                    "¿Seguro que desea anular esta venta?\n" +
                    "Se devolverá el stock correspondiente y la venta quedará marcada como Anulada.",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var motivoWindow = new MotivoAnulacionWindow();
                if (motivoWindow.ShowDialog() == true)
                {
                    string motivo = motivoWindow.Motivo;
                    _ventaService.AnularVenta(venta.Id, motivo);
                    MessageBox.Show("Venta anulada correctamente.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarVentas();
                }
            }
        }

        private void btnRefrescar_Click(object sender, RoutedEventArgs e) => CargarVentas();

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}