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
    // Ventana principal para la gestión de productos.
    // Permite buscar, agregar, editar, eliminar y paginar productos.
    // Incluye integración con escáner de código de barras y protección contra entradas residuales.
    public partial class ProductoWindow : Window
    {
        // Servicios para acceder a productos, categorías y proveedores
        private readonly ProductoService _productoService;
        private readonly CategoriaService _categoriaService;
        private readonly ProveedorService _proveedorService;

        // Producto actualmente seleccionado en la grilla
        private Producto _productoSeleccionado;

        // Variables para paginación de la grilla de productos
        private List<Producto> _allProductos = new();
        private int _currentPage = 1;
        private readonly int _pageSize = 7; // Cantidad de filas por página
        private int _totalPages = 1;

        // Variables para manejo de escáner y supresión de entradas residuales
        private DateTime _lastScanTime = DateTime.MinValue;
        private string? _lastScanCode;
        private bool _ventanaEdicionAbierta;
        private DateTime _ignoreScannerUntil = DateTime.MinValue;
        private bool _suppressScannerInput;
        private readonly DispatcherTimer _suppressTimer;

        public ProductoWindow()
        {
            InitializeComponent();
            string connectionString = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(connectionString);
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString);

            // Timer para limpiar el flag de supresión de input tras escaneo
            _suppressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _suppressTimer.Tick += (s, e) =>
            {
                _suppressScannerInput = false;
                _suppressTimer.Stop();
            };

            // Bloquea teclas residuales en el TextBox de búsqueda tras escaneo
            txtBuscar.PreviewTextInput += TxtBuscar_PreviewTextInput;
            txtBuscar.PreviewKeyDown += TxtBuscar_PreviewKeyDown;

            CargarProductos();
        }

        // Bloquea la entrada de texto si está activo el flag de supresión
        private void TxtBuscar_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (_suppressScannerInput)
                e.Handled = true;
        }

        // Bloquea cualquier tecla si está activo el flag de supresión
        private void TxtBuscar_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (_suppressScannerInput)
                e.Handled = true;
        }

        // Carga todos los productos y aplica paginación
        private void CargarProductos()
        {
            _allProductos = _productoService.ObtenerTodos().ToList();
            _currentPage = 1;
            _productoSeleccionado = null;
            AplicarPaginacion();
            UpdateButtonsState();
        }

        // Aplica la paginación sobre la lista de productos y actualiza la grilla
        private void AplicarPaginacion()
        {
            if (_allProductos == null) _allProductos = new List<Producto>();
            _totalPages = Math.Max(1, (_allProductos.Count + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            var pageItems = _allProductos.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            dgProductos.ItemsSource = null;
            dgProductos.ItemsSource = pageItems;

            // Limpia la selección al cambiar de página
            dgProductos.UnselectAll();
            _productoSeleccionado = null;

            ActualizarControlesPaginacion();
            UpdateButtonsState();
        }

        // Actualiza los controles de paginación (botones y texto de página)
        private void ActualizarControlesPaginacion()
        {
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            txtPageInfo.Text = $"Página {_currentPage} de {_totalPages}  ({_allProductos.Count} items)";
        }

        // Navega a la página anterior
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                AplicarPaginacion();
            }
        }

        // Navega a la página siguiente
        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                AplicarPaginacion();
            }
        }

        // Busca productos según el texto ingresado y reinicia la paginación
        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
            {
                _allProductos = _productoService.Filtrar(txtBuscar.Text).ToList();
                _currentPage = 1;
                AplicarPaginacion();
            }
        }

        // Abre la ventana de edición para agregar un nuevo producto
        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoEdicionWindow(DatabaseInitializer.GetConnectionString());
            if (win.ShowDialog() == true)
            {
                _productoService.AgregarProducto(win.Producto);
                CargarProductos();
            }
        }

        // Abre la ventana de edición para actualizar el producto seleccionado
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto para actualizar.");
                return;
            }

            // Se pasa una copia para evitar modificar el objeto si se cancela
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

        // Elimina el producto seleccionado tras confirmación
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un producto para eliminar.");
                return;
            }

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

        // Maneja el cambio de selección en la grilla de productos
        private void dgProductos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgProductos.SelectedItem is Producto producto)
                _productoSeleccionado = producto;
            else
                _productoSeleccionado = null;

            UpdateButtonsState();
        }

        // Limpia la selección de la grilla y deshabilita botones relacionados
        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _productoSeleccionado = null;
            dgProductos.UnselectAll();
            UpdateButtonsState();
        }

        // Filtra productos en tiempo real al cambiar el texto de búsqueda
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
                _allProductos = _productoService.Filtrar(texto).ToList();
                _currentPage = 1;
                AplicarPaginacion();
            }
        }

        // Procesa un código escaneado desde el BarcodeScannerService
        public void HandleScannedCode(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return;

            // Normaliza el código: elimina espacios y caracteres de control
            codigo = new string((codigo ?? string.Empty).Trim().Where(c => !char.IsControl(c)).ToArray());
            if (string.IsNullOrWhiteSpace(codigo)) return;

            var ahora = DateTime.UtcNow;

            // Ignora escaneos durante el periodo de supresión tras acciones de UI
            if (ahora < _ignoreScannerUntil) return;

            // Detecta concatenaciones donde el mismo código aparece al inicio y al final
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

            // Evita múltiples procesados si el mismo código se repite muy rápido
            if (_lastScanCode == codigo && (ahora - _lastScanTime).TotalMilliseconds < 500)
                return;

            _lastScanCode = codigo;
            _lastScanTime = ahora;

            // Activa supresión de input y reinicia el timer
            _suppressScannerInput = true;
            _suppressTimer.Stop();
            _suppressTimer.Start();

            // Ejecuta en el hilo de UI: quita foco, escribe el código y busca
            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Windows.Input.Keyboard.ClearFocus();
                }
                catch { /* no crítico */ }

                bool prevReadOnly = txtBuscar.IsReadOnly;
                try
                {
                    txtBuscar.IsReadOnly = true;
                    txtBuscar.Text = codigo;
                    txtBuscar.CaretIndex = codigo.Length;
                    _allProductos = _productoService.Filtrar(codigo).ToList();
                    _currentPage = 1;
                    AplicarPaginacion();
                }
                finally
                {
                    txtBuscar.IsReadOnly = prevReadOnly;
                    this.Focus();
                }
            });
        }

        // Habilita o deshabilita los botones según si hay un producto seleccionado
        private void UpdateButtonsState()
        {
            bool hasSelection = _productoSeleccionado != null;
            btnActualizar.IsEnabled = hasSelection;
            btnEliminar.IsEnabled = hasSelection;
            btnLimpiar.IsEnabled = hasSelection;
        }
    }
}