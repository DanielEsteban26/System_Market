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
    }
}