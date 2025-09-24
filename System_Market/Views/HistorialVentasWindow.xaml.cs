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
    // Ventana que muestra el historial de ventas realizadas en el sistema.
    // Permite filtrar por fechas, paginar resultados y anular ventas.
    public partial class HistorialVentasWindow : Window
    {
        // Servicio para acceder a las operaciones de ventas en la base de datos
        private readonly VentaService _ventaService;
        // Lista completa de ventas obtenidas de la base de datos (cache en memoria)
        private List<Venta> _allVentas = new();

        // Paginación
        // Lista filtrada según el rango de fechas seleccionado
        private List<Venta> _filteredVentas = new();
        // Página actual mostrada en la grilla
        private int _currentPage = 1;
        // Cantidad de ventas por página
        private readonly int _pageSize = 5;
        // Total de páginas calculadas según el filtro
        private int _totalPages = 1;

        // Constructor: inicializa componentes, servicio y valores por defecto de los filtros
        public HistorialVentasWindow()
        {
            InitializeComponent();
            _ventaService = new VentaService(DatabaseInitializer.GetConnectionString());

            // Por defecto, muestra las ventas de la última semana
            dpDesdeVenta.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaVenta.SelectedDate = DateTime.Today;

            CargarVentas();
        }

        // Carga todas las ventas desde la base de datos y aplica el filtro inicial
        private void CargarVentas()
        {
            // Trae todas las ventas ordenadas por fecha descendente
            _allVentas = _ventaService.ObtenerTodas()
                .OrderByDescending(v => v.Fecha)
                .ToList();

            // Limpia la grilla de detalles y deshabilita el botón de anular
            dgDetallesVenta.ItemsSource = null;
            btnAnularVenta.IsEnabled = false;

            // Aplica el filtro de fechas y muestra la primera página
            AplicarFiltroYPaginarVenta();
        }

        // Aplica el filtro de fechas (desde/hasta) sobre la lista de ventas
        private void AplicarFiltroVenta()
        {
            // Obtiene las fechas seleccionadas en los DatePickers
            var desde = (dpDesdeVenta.SelectedDate ?? DateTime.Today).Date;
            var hasta = (dpHastaVenta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

            // Filtra las ventas por el rango de fechas
            _filteredVentas = _allVentas
                .Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                .OrderByDescending(v => v.Fecha)
                .ToList();
        }

        // Aplica el filtro de fechas y reinicia la paginación
        private void AplicarFiltroYPaginarVenta()
        {
            AplicarFiltroVenta();
            _currentPage = 1;
            AplicarPaginacionVenta();
        }

        // Muestra la página actual de ventas en la grilla y actualiza controles de navegación
        private void AplicarPaginacionVenta()
        {
            // Calcula el total de páginas según la cantidad de ventas filtradas
            _totalPages = Math.Max(1, (_filteredVentas.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            // Obtiene solo los elementos de la página actual
            var pageItems = _filteredVentas
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            // Asigna los datos a la grilla de ventas
            dgVentas.ItemsSource = null;
            dgVentas.ItemsSource = pageItems;

            // Habilita/deshabilita los botones de navegación según la página
            btnPrevPageVenta.IsEnabled = _currentPage > 1;
            btnNextPageVenta.IsEnabled = _currentPage < _totalPages;
            txtPageInfoVenta.Text = $"Página {_currentPage} de {_totalPages}  ({_filteredVentas.Count} items)";
        }

        // Evento: al seleccionar una venta, muestra sus detalles y habilita el botón de anular si está activa
        private void dgVentas_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var venta = dgVentas.SelectedItem as Venta;
            if (venta != null)
            {
                // Muestra los detalles de la venta seleccionada
                dgDetallesVenta.ItemsSource = _ventaService.ObtenerDetallesPorVenta(venta.Id);
                // Solo permite anular si la venta está activa
                btnAnularVenta.IsEnabled = venta.Estado == "Activa";
            }
            else
            {
                dgDetallesVenta.ItemsSource = null;
                btnAnularVenta.IsEnabled = false;
            }
        }

        // Evento: al hacer clic en "Anular venta", solicita confirmación y motivo, y anula la venta
        private void btnAnularVenta_Click(object sender, RoutedEventArgs e)
        {
            var venta = dgVentas.SelectedItem as Venta;
            // Solo permite anular si la venta está activa
            if (venta == null || venta.Estado != "Activa") return;

            // Solicita confirmación al usuario
            if (MessageBox.Show(
                    "¿Seguro que desea anular esta venta?\n" +
                    "Se devolverá el stock correspondiente y la venta quedará marcada como Anulada.",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Solicita el motivo de anulación en una ventana aparte
                var motivoWindow = new MotivoAnulacionWindow();
                if (motivoWindow.ShowDialog() == true)
                {
                    string motivo = motivoWindow.Motivo;
                    // Anula la venta y actualiza la base de datos
                    _ventaService.AnularVenta(venta.Id, motivo);
                    MessageBox.Show("Venta anulada correctamente.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Recarga la lista de ventas para reflejar el cambio
                    CargarVentas();
                }
            }
        }

        // Refresca la lista de ventas desde la base de datos
        private void btnRefrescar_Click(object sender, RoutedEventArgs e) => CargarVentas();

        // Cierra la ventana de historial de ventas
        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        // Aplica el filtro de fechas cuando se hace clic en el botón correspondiente
        private void BtnAplicarFiltroVenta_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltroYPaginarVenta();
        }

        // Navega a la página anterior de ventas
        private void BtnPrevPageVenta_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                AplicarPaginacionVenta();
            }
        }

        // Navega a la página siguiente de ventas
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