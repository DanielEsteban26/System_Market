using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    /// <summary>
    /// Lógica de interacción para ProductoWindow.xaml
    /// </summary>
    public partial class ProductoWindow : Window
    {
        private readonly ProductoService _productoService;
        private readonly CategoriaService _categoriaService;
        private readonly ProveedorService _proveedorService;
        private Producto _productoSeleccionado;

        // --- NUEVO CÓDIGO PARA PAGINACIÓN ---
        private List<Producto> _allProductos = new();
        private int _currentPage = 1;
        private readonly int _pageSize = 7; // <-- filas por página
        private int _totalPages = 1;
        // --- FIN NUEVO CÓDIGO ---

        // --- EXISTENTE ---
        private DateTime _lastScanTime = DateTime.MinValue;
        private string? _lastScanCode;
        private bool _ventanaEdicionAbierta;
        private DateTime _ignoreScannerUntil = DateTime.MinValue;

        // --- NUEVO: supresión temporal de input para evitar "residuos" del scanner ---
        private bool _suppressScannerInput;
        private readonly DispatcherTimer _suppressTimer;

        public ProductoWindow()
        {
            InitializeComponent();
            string connectionString = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(connectionString);
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString); // mantener como estaba en tu proyecto

            // Inicializar timer para limpiar flag de supresión
            _suppressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _suppressTimer.Tick += (s, e) =>
            {
                _suppressScannerInput = false;
                _suppressTimer.Stop();
            };

            // Interceptar input en el TextBox para bloquear teclas residuales
            txtBuscar.PreviewTextInput += TxtBuscar_PreviewTextInput;
            txtBuscar.PreviewKeyDown += TxtBuscar_PreviewKeyDown;

            CargarProductos();
        }

        private void TxtBuscar_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (_suppressScannerInput)
            {
                e.Handled = true;
            }
        }

        private void TxtBuscar_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (_suppressScannerInput)
            {
                // bloquear cualquier tecla mientras esté activo el flag
                e.Handled = true;
            }
        }

        private void CargarProductos()
        {
            // Cargamos todos los productos en memoria y aplicamos paginación
            _allProductos = _productoService.ObtenerTodos().ToList();
            _currentPage = 1;
            _productoSeleccionado = null;
            AplicarPaginacion();
            UpdateButtonsState();
        }

        private void AplicarPaginacion()
        {
            if (_allProductos == null) _allProductos = new List<Producto>();
            _totalPages = Math.Max(1, (_allProductos.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            var pageItems = _allProductos.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            dgProductos.ItemsSource = null; // fuerza rebind
            dgProductos.ItemsSource = pageItems;

            // Al cambiar de página limpiamos selección para evitar referencia inválida
            dgProductos.UnselectAll();
            _productoSeleccionado = null;

            ActualizarControlesPaginacion();
            UpdateButtonsState();
        }

        private void ActualizarControlesPaginacion()
        {
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            txtPageInfo.Text = $"Página {_currentPage} de {_totalPages}  ({_allProductos.Count} items)";
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

        // --- Métodos existentes adaptados a paginación ---

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
            {
                _allProductos = _productoService.Filtrar(txtBuscar.Text).ToList();
                _currentPage = 1;
                AplicarPaginacion();
            }
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoEdicionWindow(DatabaseInitializer.GetConnectionString());
            if (win.ShowDialog() == true)
            {
                _productoService.AgregarProducto(win.Producto);
                CargarProductos();
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto para actualizar.");
                return;
            }

            // Pasa una copia para evitar modificar el objeto si se cancela
            var productoCopia = new Producto
            {
                Id = _productoSeleccionado.Id,
                CodigoBarras = _productoSeleccionado.CodigoBarras,
                Nombre = _productoSeleccionado.Nombre,
                CategoriaId = _productoSeleccionado.CategoriaId,
                ProveedorId = _productoSeleccionado.ProveedorId,
                PrecioCompra = _productoSeleccionado.PrecioCompra,
                PrecioVenta = _productoSeleccionado.PrecioVenta,
                Stock = _productoSeleccionado.Stock
            };

            var win = new ProductoEdicionWindow(DatabaseInitializer.GetConnectionString(), productoCopia);
            if (win.ShowDialog() == true)
            {
                _productoService.ActualizarProducto(win.Producto);
                CargarProductos();
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto para eliminar.");
                return;
            }

            // Confirmación antes de eliminar
            var confirm = MessageBox.Show(
                $"¿Está seguro que desea eliminar el producto:\n\n" +
                $"Código: {_productoSeleccionado.CodigoBarras}\n" +
                $"Nombre: {_productoSeleccionado.Nombre}\n" +
                $"Categoría: {_productoSeleccionado.CategoriaNombre}\n" +
                $"Proveedor: {_productoSeleccionado.ProveedorNombre}\n\n" +
                $"Esta acción no se puede deshacer.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _productoService.EliminarProducto(_productoSeleccionado.Id);
            CargarProductos();
            _productoSeleccionado = null;
            UpdateButtonsState();
        }

        private void dgProductos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgProductos.SelectedItem is Producto producto)
            {
                _productoSeleccionado = producto;
            }
            else
            {
                _productoSeleccionado = null;
            }

            UpdateButtonsState();
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _productoSeleccionado = null;
            dgProductos.UnselectAll();
            UpdateButtonsState();
        }

        private void txtBuscar_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string texto = txtBuscar.Text;
            if (string.IsNullOrWhiteSpace(texto))
            {
                _allProductos = _productoService.ObtenerTodos().ToList();
                _currentPage = 1;
                AplicarPaginacion();
            }
            else
            {
                _allProductos = _productoService.Filtrar(texto).ToList(); // mantener llamado al service
                _currentPage = 1;
                AplicarPaginacion();
            }
        }

        // --- ESCÁNER: llamado desde BarcodeScannerService cuando esta ventana está activa ---
        public void HandleScannedCode(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return;

            // Normalizar: trim + eliminar controles
            codigo = new string((codigo ?? string.Empty).Trim().Where(c => !char.IsControl(c)).ToArray());
            if (string.IsNullOrWhiteSpace(codigo)) return;

            var ahora = DateTime.UtcNow;

            // Ignorar scans durante el periodo tras acciones de UI
            if (ahora < _ignoreScannerUntil) return;

            // Detectar concatenaciones donde el mismo código aparece al inicio y al final
            int[] plausibleLengths = { 8, 12, 13, 14, 18 };
            foreach (var len in plausibleLengths)
            {
                if (codigo.Length >= len * 2)
                {
                    var start = codigo.Substring(0, len);
                    var end = codigo.Substring(codigo.Length - len, len);
                    if (string.Equals(start, end, StringComparison.Ordinal))
                    {
                        codigo = end;
                        break;
                    }
                }
            }

            // Evitar múltiples procesados si el mismo código se repite muy rápido
            if (_lastScanCode == codigo && (ahora - _lastScanTime).TotalMilliseconds < 500)
                return;

            _lastScanCode = codigo;
            _lastScanTime = ahora;

            // Marcar supresión de input y reiniciar timer
            _suppressScannerInput = true;
            _suppressTimer.Stop();
            _suppressTimer.Start();

            // Ejecutar en UI thread: quitar foco del textbox, escribir el código (sobrescribiendo) y buscar.
            Dispatcher.Invoke(() =>
            {
                // 1) Quitar foco del TextBox para que las teclas del scanner no sean dirigidas a él
                try
                {
                    System.Windows.Input.Keyboard.ClearFocus();
                }
                catch { /* no crítico */ }

                // 2) Proteger edición temporalmente
                bool prevReadOnly = txtBuscar.IsReadOnly;
                try
                {
                    txtBuscar.IsReadOnly = true;         // evita inserciones mientras se escribe programáticamente
                    txtBuscar.Text = codigo;             // sobrescribe cualquier contenido anterior
                    txtBuscar.CaretIndex = codigo.Length;
                    // 3) Lanzar búsqueda con el nuevo valor (mantiene comportamiento de paginación)
                    _allProductos = _productoService.Filtrar(codigo).ToList();
                    _currentPage = 1;
                    AplicarPaginacion();
                }
                finally
                {
                    txtBuscar.IsReadOnly = prevReadOnly; // restaurar estado
                    // Mantener foco fuera del textbox para minimizar riesgo de teclas residuales
                    this.Focus();
                }
            });
        }

        // Actualiza disponibilidad de botones según selección
        private void UpdateButtonsState()
        {
            bool hasSelection = _productoSeleccionado != null;
            btnActualizar.IsEnabled = hasSelection;
            btnEliminar.IsEnabled = hasSelection;
            btnLimpiar.IsEnabled = hasSelection;
        }
    }
}