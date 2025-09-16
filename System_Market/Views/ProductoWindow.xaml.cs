using System;
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

        private void BtnRefrescar_Click(object sender, RoutedEventArgs e)
        {
            CargarProductos();
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
            codigo = codigo.Trim();

            // Evitar múltiples ventanas si el mismo código se repite muy rápido
            var ahora = DateTime.UtcNow;
            if (_lastScanCode == codigo && (ahora - _lastScanTime).TotalMilliseconds < 500)
                return;

            _lastScanCode = codigo;
            _lastScanTime = ahora;

            try
            {
                // Intentar localizar producto por código de barras
                var prod = _productoService.ObtenerPorCodigoBarras(codigo);
                if (prod != null)
                {
                    // Mostrarlo solo
                    txtBuscar.Text = codigo;
                    dgProductos.ItemsSource = new List<Producto> { prod };
                    dgProductos.SelectedItem = prod;
                    dgProductos.ScrollIntoView(prod);
                    return;
                }

                // Si ya hay una ventana de edición abierta por un escaneo previo, ignorar este
                if (_ventanaEdicionAbierta) return;

                _ventanaEdicionAbierta = true;

                // Abrir creación con el código prellenado y bloqueado
                var win = new ProductoEdicionWindow(
                    DatabaseInitializer.GetConnectionString(),
                    producto: null,
                    codigoPrefill: codigo,
                    bloquearCodigo: true)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var ok = win.ShowDialog() == true && win.Producto != null;
                _ventanaEdicionAbierta = false;

                if (ok)
                {
                    try
                    {
                        _productoService.AgregarProducto(win.Producto);
                        // Recargar listado completo y seleccionar el nuevo
                        CargarProductos();
                        var lista = dgProductos.ItemsSource as IEnumerable<Producto>;
                        var recien = lista?.FirstOrDefault(p => p.Id == win.Producto.Id);
                        if (recien != null)
                        {
                            dgProductos.SelectedItem = recien;
                            dgProductos.ScrollIntoView(recien);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error guardando producto: " + ex.Message,
                            "Producto", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Si se cancela, restaurar búsqueda vacía para volver a la lista
                    if (string.Equals(txtBuscar.Text, codigo, StringComparison.OrdinalIgnoreCase))
                    {
                        txtBuscar.Clear();
                        CargarProductos();
                    }
                }
            }
            catch (Exception ex)
            {
                _ventanaEdicionAbierta = false;
                MessageBox.Show("Error procesando escaneo: " + ex.Message,
                    "Escáner", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}