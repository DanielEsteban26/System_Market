using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class HistorialComprasWindow : Window
    {
        private readonly CompraService _compraService;

        // Paginación y cache en memoria
        private List<Compra> _allCompras = new();
        private List<Compra> _filteredCompras = new();
        private int _currentPage = 1;
        private readonly int _pageSize = 5;
        private int _totalPages = 1;

        public HistorialComprasWindow()
        {
            InitializeComponent();
            _compraService = new CompraService(DatabaseInitializer.GetConnectionString());
            // Inicializar pickers por defecto
            dpDesdeCompra.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaCompra.SelectedDate = DateTime.Today;
            CargarCompras();
        }

        private void CargarCompras()
        {
            _allCompras = _compraService.ObtenerTodas()
                .OrderByDescending(c => c.Fecha)
                .ToList();
            dgDetallesCompra.ItemsSource = null;
            btnAnularCompra.IsEnabled = false;

            // Aplicar filtro inicial y paginar
            AplicarFiltroYPaginar();
        }

        // Aplica el filtro de fechas (desde/hasta) y reinicia paginación
        private void AplicarFiltroYPaginar()
        {
            var desde = (dpDesdeCompra.SelectedDate ?? DateTime.Today).Date;
            var hasta = (dpHastaCompra.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

            _filteredCompras = _allCompras
                .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
                .OrderByDescending(c => c.Fecha)
                .ToList();

            _currentPage = 1;
            AplicarPaginacion();
        }

        private void AplicarPaginacion()
        {
            _totalPages = Math.Max(1, (_filteredCompras.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            var pageItems = _filteredCompras
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgCompras.ItemsSource = null;
            dgCompras.ItemsSource = pageItems;

            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            txtPageInfo.Text = $"Página {_currentPage} de {_totalPages}  ({_filteredCompras.Count} items)";
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

        private void BtnAplicarFiltroCompra_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltroYPaginar();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                AplicarPaginacion();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                AplicarPaginacion();
            }
        }
    }
}