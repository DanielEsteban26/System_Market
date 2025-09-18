using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class HistorialVentasWindow : Window
    {
        private readonly VentaService _ventaService;
        private List<Venta> _allVentas = new();

        // Paginación
        private List<Venta> _filteredVentas = new();
        private int _currentPage = 1;
        private readonly int _pageSize = 5;
        private int _totalPages = 1;

        public HistorialVentasWindow()
        {
            InitializeComponent();
            _ventaService = new VentaService(DatabaseInitializer.GetConnectionString());

            // inicializar pickers por defecto
            dpDesdeVenta.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaVenta.SelectedDate = DateTime.Today;

            CargarVentas();
        }

        private void CargarVentas()
        {
            _allVentas = _ventaService.ObtenerTodas()
                .OrderByDescending(v => v.Fecha)
                .ToList();

            dgDetallesVenta.ItemsSource = null;
            btnAnularVenta.IsEnabled = false;

            // Mostrar rango por defecto (últimos 7 días) y paginar
            AplicarFiltroYPaginarVenta();
        }

        private void AplicarFiltroVenta()
        {
            var desde = (dpDesdeVenta.SelectedDate ?? DateTime.Today).Date;
            var hasta = (dpHastaVenta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

            _filteredVentas = _allVentas
                .Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                .OrderByDescending(v => v.Fecha)
                .ToList();
        }

        // Combina filtro + paginación
        private void AplicarFiltroYPaginarVenta()
        {
            AplicarFiltroVenta();
            _currentPage = 1;
            AplicarPaginacionVenta();
        }

        private void AplicarPaginacionVenta()
        {
            _totalPages = Math.Max(1, (_filteredVentas.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            var pageItems = _filteredVentas
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgVentas.ItemsSource = null;
            dgVentas.ItemsSource = pageItems;

            btnPrevPageVenta.IsEnabled = _currentPage > 1;
            btnNextPageVenta.IsEnabled = _currentPage < _totalPages;
            txtPageInfoVenta.Text = $"Página {_currentPage} de {_totalPages}  ({_filteredVentas.Count} items)";
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

        private void BtnAplicarFiltroVenta_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltroYPaginarVenta();
        }

        private void BtnPrevPageVenta_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                AplicarPaginacionVenta();
            }
        }

        private void BtnNextPageVenta_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                AplicarPaginacionVenta();
            }
        }
    }
}