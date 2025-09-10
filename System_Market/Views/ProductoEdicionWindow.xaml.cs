using System.Windows;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class ProductoEdicionWindow : Window
    {
        public Producto Producto { get; private set; }
        private readonly CategoriaService _categoriaService;
        private readonly ProveedorService _proveedorService;

        public ProductoEdicionWindow(string connectionString, Producto producto = null)
        {
            InitializeComponent();
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString);

            cbCategoria.ItemsSource = _categoriaService.ObtenerTodas();
            cbProveedor.ItemsSource = _proveedorService.ObtenerTodos();

            if (producto != null)
            {
                Producto = producto;
                txtCodigoBarras.Text = producto.CodigoBarras;
                txtNombre.Text = producto.Nombre;
                cbCategoria.SelectedValue = producto.CategoriaId;
                cbProveedor.SelectedValue = producto.ProveedorId;
                txtPrecioCompra.Text = producto.PrecioCompra.ToString();
                txtPrecioVenta.Text = producto.PrecioVenta.ToString();
                txtStock.Text = producto.Stock.ToString();
                Title = "Editar Producto";
            }
            else
            {
                Title = "Agregar Producto";
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodigoBarras.Text) ||
                string.IsNullOrWhiteSpace(txtNombre.Text) ||
                cbCategoria.SelectedValue == null ||
                cbProveedor.SelectedValue == null ||
                !decimal.TryParse(txtPrecioCompra.Text, out decimal precioCompra) ||
                !decimal.TryParse(txtPrecioVenta.Text, out decimal precioVenta) ||
                !int.TryParse(txtStock.Text, out int stock))
            {
                MessageBox.Show("Complete todos los campos correctamente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Producto = Producto ?? new Producto();
            Producto.CodigoBarras = txtCodigoBarras.Text.Trim();
            Producto.Nombre = txtNombre.Text.Trim();
            Producto.CategoriaId = (int)cbCategoria.SelectedValue;
            Producto.ProveedorId = (int)cbProveedor.SelectedValue;
            Producto.PrecioCompra = precioCompra;
            Producto.PrecioVenta = precioVenta;
            Producto.Stock = stock;

            if (Producto.Stock < 0)
            {
                MessageBox.Show("El stock no puede ser negativo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}