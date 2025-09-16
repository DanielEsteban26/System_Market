using System;
using System.Linq;
using System.Windows;
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

        // --- NUEVO CÓDIGO ---
        private DateTime _lastScanTime = DateTime.MinValue;
        private string? _lastScanCode;
        private bool _ventanaEdicionAbierta;
        private DateTime _ignoreScannerUntil = DateTime.MinValue;
        // --- FIN NUEVO CÓDIGO ---

        public ProductoWindow()
        {
            InitializeComponent();
            string connectionString = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(connectionString);
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString);

            CargarProductos();
        }

   
        private void CargarProductos()
        {
            var data = _productoService.ObtenerTodos();
            dgProductos.ItemsSource = null;           // fuerza rebind
            dgProductos.ItemsSource = data;
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
            {
                var data = _productoService.Filtrar(txtBuscar.Text);
                dgProductos.ItemsSource = null;
                dgProductos.ItemsSource = data;
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
        }

        private void dgProductos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgProductos.SelectedItem is Producto producto)
            {
                _productoSeleccionado = producto;
            }
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _productoSeleccionado = null;
            dgProductos.UnselectAll();
        }

        private void txtBuscar_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string texto = txtBuscar.Text;
            if (string.IsNullOrWhiteSpace(texto))
            {
                dgProductos.ItemsSource = _productoService.ObtenerTodos();
            }
            else
            {
                dgProductos.ItemsSource = _productoService.Filtrar(texto);
            }
        }

        // --- ESCÁNER: llamado desde BarcodeScannerService cuando esta ventana está activa ---
        public void HandleScannedCode(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return;

            // Normalizar entrada rápida
            codigo = codigo.Trim();
            var ahora = DateTime.UtcNow;

            // Ignorar scans durante el periodo tras acciones de UI (p. ej. al pulsar Refrescar)
            if (ahora < _ignoreScannerUntil) return;

            // Evitar múltiples ventanas si el mismo código se repite muy rápido
            if (_lastScanCode == codigo && (ahora - _lastScanTime).TotalMilliseconds < 500)
                return;

            _lastScanCode = codigo;
            _lastScanTime = ahora;

            // Poner el código en el buscador y lanzar la búsqueda
            txtBuscar.Text = codigo;
            BtnBuscar_Click(txtBuscar, new RoutedEventArgs());
        }
    }
}