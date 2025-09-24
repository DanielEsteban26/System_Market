// Referencias a librerías base, WPF y servicios propios del sistema
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    // Ventana que muestra el historial de compras realizadas en el sistema.
    // Permite filtrar por fechas, paginar resultados y anular compras.
    public partial class HistorialComprasWindow : Window
    {
        // Servicio para acceder a las operaciones de compras en la base de datos
        private readonly CompraService _compraService;

        // Lista completa de compras obtenidas de la base de datos (cache en memoria)
        private List<Compra> _allCompras = new();
        // Lista filtrada según el rango de fechas seleccionado
        private List<Compra> _filteredCompras = new();
        // Página actual mostrada en la grilla
        private int _currentPage = 1;
        // Cantidad de compras por página
        private readonly int _pageSize = 5;
        // Total de páginas calculadas según el filtro
        private int _totalPages = 1;

        // Constructor: inicializa componentes, servicio y valores por defecto de los filtros
        public HistorialComprasWindow()
        {
            InitializeComponent();
            _compraService = new CompraService(DatabaseInitializer.GetConnectionString());
            // Por defecto, muestra las compras de la última semana
            dpDesdeCompra.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaCompra.SelectedDate = DateTime.Today;
            CargarCompras();
        }

        // Carga todas las compras desde la base de datos y aplica el filtro inicial
        private void CargarCompras()
        {
            // Trae todas las compras ordenadas por fecha descendente
            _allCompras = _compraService.ObtenerTodas()
                .OrderByDescending(c => c.Fecha)
                .ToList();
            // Limpia la grilla de detalles y deshabilita el botón de anular
            dgDetallesCompra.ItemsSource = null;
            btnAnularCompra.IsEnabled = false;

            // Aplica el filtro de fechas y muestra la primera página
            AplicarFiltroYPaginar();
        }

        // Aplica el filtro de fechas (desde/hasta) y reinicia la paginación
        private void AplicarFiltroYPaginar()
        {
            // Obtiene las fechas seleccionadas en los DatePickers
            var desde = (dpDesdeCompra.SelectedDate ?? DateTime.Today).Date;
            var hasta = (dpHastaCompra.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

            // Filtra las compras por el rango de fechas
            _filteredCompras = _allCompras
                .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
                .OrderByDescending(c => c.Fecha)
                .ToList();

            // Reinicia a la primera página y aplica la paginación
            _currentPage = 1;
            AplicarPaginacion();
        }

        // Muestra la página actual de compras en la grilla y actualiza controles de navegación
        private void AplicarPaginacion()
        {
            // Calcula el total de páginas según la cantidad de compras filtradas
            _totalPages = Math.Max(1, (_filteredCompras.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            // Obtiene solo los elementos de la página actual
            var pageItems = _filteredCompras
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            // Asigna los datos a la grilla de compras
            dgCompras.ItemsSource = null;
            dgCompras.ItemsSource = pageItems;

            // Habilita/deshabilita los botones de navegación según la página
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            txtPageInfo.Text = $"Página {_currentPage} de {_totalPages}  ({_filteredCompras.Count} items)";
        }

        // Evento: al seleccionar una compra, muestra sus detalles y habilita el botón de anular si está activa
        private void dgCompras_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var compra = dgCompras.SelectedItem as Compra;
            if (compra != null)
            {
                // Muestra los detalles de la compra seleccionada
                dgDetallesCompra.ItemsSource = _compraService.ObtenerDetallesPorCompra(compra.Id);
                // Solo permite anular si la compra está activa
                btnAnularCompra.IsEnabled = compra.Estado == "Activa";
            }
            else
            {
                dgDetallesCompra.ItemsSource = null;
                btnAnularCompra.IsEnabled = false;
            }
        }

        // Evento: al hacer clic en "Anular compra", solicita confirmación y motivo, y anula la compra
        private void btnAnularCompra_Click(object sender, RoutedEventArgs e)
        {
            var compra = dgCompras.SelectedItem as Compra;
            // Solo permite anular si la compra está activa
            if (compra == null || compra.Estado != "Activa") return;

            // Solicita confirmación al usuario
            if (MessageBox.Show(
                    "¿Seguro que desea anular esta compra?\n" +
                    "Se revertirá el stock de los productos y la compra quedará marcada como Anulada.",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Solicita el motivo de anulación en una ventana aparte
                var motivoWindow = new MotivoAnulacionWindow();
                if (motivoWindow.ShowDialog() == true)
                {
                    string motivo = motivoWindow.Motivo;
                    // Anula la compra y actualiza la base de datos
                    _compraService.AnularCompra(compra.Id, motivo);
                    MessageBox.Show("Compra anulada correctamente.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Recarga la lista de compras para reflejar el cambio
                    CargarCompras();
                }
            }
        }

        // Refresca la lista de compras desde la base de datos
        private void btnRefrescar_Click(object sender, RoutedEventArgs e) => CargarCompras();

        // Cierra la ventana de historial de compras
        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        // Aplica el filtro de fechas cuando se hace clic en el botón correspondiente
        private void BtnAplicarFiltroCompra_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltroYPaginar();
        }

        // Navega a la página anterior de compras
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                AplicarPaginacion();
            }
        }

        // Navega a la página siguiente de compras
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