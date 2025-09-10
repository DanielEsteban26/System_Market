using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class CompraWindow : Window
    {
        private readonly CompraService _compraService;
        private readonly ProveedorService _proveedor_service;
        private readonly ProductoService _productoService;

        private readonly List<DetalleCompra> _detalles = new();

        private const string Simbolo = "S/";
        private static readonly Regex RegexEntero = new(@"^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex RegexDecimal = new(@"^[0-9\.,]+$", RegexOptions.Compiled);

        public CompraWindow()
        {
            InitializeComponent();

            string conn = DatabaseInitializer.GetConnectionString();
            _compraService = new CompraService(conn);
            _proveedor_service = new ProveedorService(conn);
            _productoService = new ProductoService(conn);

            CargarCombos();
        }

        private void CargarCombos()
        {
            var proveedores = _proveedor_service.ObtenerTodos();
            cmbProveedor.ItemsSource = proveedores;
            cmbProveedor.DisplayMemberPath = "Nombre";
            cmbProveedor.SelectedValuePath = "Id";

            var productos = _productoService.ObtenerTodos();
            cmbProducto.ItemsSource = productos;
            cmbProducto.DisplayMemberPath = "Nombre";
            cmbProducto.SelectedValuePath = "Id";
        }

        private void btnAgregarDetalle_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProducto.SelectedValue == null)
            {
                MessageBox.Show("Seleccione un producto.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtCantidad.Text.Trim(), out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingrese una cantidad válida.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParsePrecio(txtPrecioUnitario.Text.Trim(), out decimal precio) || precio < 0)
            {
                MessageBox.Show("Ingrese un precio válido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int productoId = Convert.ToInt32(cmbProducto.SelectedValue);
            var producto = (Producto)cmbProducto.SelectedItem;

            var existente = _detalles.FirstOrDefault(d => d.ProductoId == productoId && d.PrecioUnitario == precio);
            if (existente != null)
            {
                existente.Cantidad += cantidad;
                existente.Subtotal = existente.Cantidad * existente.PrecioUnitario;
            }
            else
            {
                _detalles.Add(new DetalleCompra
                {
                    ProductoId = productoId,
                    Cantidad = cantidad,
                    PrecioUnitario = precio,
                    Subtotal = cantidad * precio,
                    ProductoNombre = producto?.Nombre ?? ""
                });
            }

            RefrescarDetalle();
            CalcularTotal();

            cmbProducto.SelectedIndex = -1;
            txtCantidad.Clear();
            txtPrecioUnitario.Clear();
            OcultarEditorPrecios();
        }

        private void RefrescarDetalle()
        {
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = _detalles;
        }

        private void CalcularTotal()
        {
            decimal total = _detalles.Sum(d => d.Subtotal);
            txtTotal.Text = $"{Simbolo} {total:N2}";
        }

        private void btnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProveedor.SelectedValue == null)
            {
                MessageBox.Show("Seleccione un proveedor.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_detalles.Count == 0)
            {
                MessageBox.Show("Agregue al menos un detalle a la compra.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var compra = new Compra
                {
                    UsuarioId = 1, // TODO: reemplazar por usuario logueado
                    ProveedorId = Convert.ToInt32(cmbProveedor.SelectedValue),
                    Fecha = DateTime.Now,
                    Total = _detalles.Sum(d => d.Subtotal),
                    Estado = "Activa",
                    MotivoAnulacion = ""
                };

                int compraId = _compraService.AgregarCompraConDetalles(compra, _detalles);

                MessageBox.Show($"Compra registrada correctamente. Id: {compraId}", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _detalles.Clear();
                RefrescarDetalle();
                txtTotal.Clear();
                OcultarEditorPrecios();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar la compra: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void btnHistorialCompras_Click(object sender, RoutedEventArgs e)
        {
            var historial = new HistorialComprasWindow { Owner = this };
            historial.ShowDialog();
        }

        private void cmbProducto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var producto = cmbProducto.SelectedItem as Producto;
            if (producto != null)
            {
                txtPrecioUnitario.Text = producto.PrecioCompra.ToString("0.00");
                if (panelEditorPrecios.Visibility == Visibility.Visible)
                {
                    txtNuevoPrecioCompra.Text = producto.PrecioCompra.ToString("0.00");
                    txtNuevoPrecioVenta.Text = producto.PrecioVenta.ToString("0.00");
                    lblProductoOverlay.Text = producto.Nombre;
                }
            }
            else
            {
                txtPrecioUnitario.Clear();
                OcultarEditorPrecios();
            }
        }

        private void btnProductoNuevo_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoEdicionWindow(DatabaseInitializer.GetConnectionString());
            if (win.ShowDialog() == true)
            {
                _productoService.AgregarProducto(win.Producto);
                var productos = _productoService.ObtenerTodos();
                cmbProducto.ItemsSource = productos;
                cmbProducto.DisplayMemberPath = "Nombre";
                cmbProducto.SelectedValuePath = "Id";
                cmbProducto.SelectedIndex = productos.Count - 1;
            }
        }

        private void BtnEditarPrecioUnitario_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProducto.SelectedItem is not Producto prodSel)
            {
                MessageBox.Show("Seleccione un producto primero.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var msg =
                "Vas a editar los precios del producto:\n" +
                $"- {prodSel.Nombre}\n\n" +
                "• Actualiza precio de COMPRA y/o VENTA.\n" +
                "• Afecta futuras operaciones.\n" +
                "• No modifica compras previas.\n\n" +
                "¿Continuar?";
            if (MessageBox.Show(msg, "Confirmar", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            lblProductoOverlay.Text = prodSel.Nombre;
            txtNuevoPrecioCompra.Text = prodSel.PrecioCompra.ToString("0.00");
            txtNuevoPrecioVenta.Text = prodSel.PrecioVenta.ToString("0.00");
            panelEditorPrecios.Visibility = Visibility.Visible;
        }

        private void BtnGuardarPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProducto.SelectedItem is not Producto prodSel)
            {
                MessageBox.Show("Seleccione un producto.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParsePrecio(txtNuevoPrecioCompra.Text, out var nuevoPC) || nuevoPC < 0)
            {
                MessageBox.Show("Precio de compra inválido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParsePrecio(txtNuevoPrecioVenta.Text, out var nuevoPV) || nuevoPV < 0)
            {
                MessageBox.Show("Precio de venta inválido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            prodSel.PrecioCompra = Math.Round(nuevoPC, 2, MidpointRounding.AwayFromZero);
            prodSel.PrecioVenta = Math.Round(nuevoPV, 2, MidpointRounding.AwayFromZero);

            try
            {
                _productoService.ActualizarProducto(prodSel);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo actualizar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtPrecioUnitario.Text = prodSel.PrecioCompra.ToString("0.00");
            MessageBox.Show("Precios actualizados.", "OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
            OcultarEditorPrecios();
        }

        private void BtnCancelarPrecios_Click(object sender, RoutedEventArgs e) => OcultarEditorPrecios();

        private void OcultarEditorPrecios() => panelEditorPrecios.Visibility = Visibility.Collapsed;

        // Parser robusto (coma / punto, con o sin 'S/')
        private static bool TryParsePrecio(string? texto, out decimal valor)
        {
            valor = 0m;
            if (string.IsNullOrWhiteSpace(texto)) return false;
            texto = texto.Replace("S/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace(" ", "")
                         .Trim()
                         .Replace(',', '.');
            var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
            var cultures = new[] { CultureInfo.CurrentCulture, new CultureInfo("es-PE"), CultureInfo.InvariantCulture };
            foreach (var c in cultures)
                if (decimal.TryParse(texto, styles, c, out valor))
                    return true;
            return false;
        }

        #region Validaciones Input
        private void OnCantidadPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !RegexEntero.IsMatch(e.Text);
        }

        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !RegexDecimal.IsMatch(e.Text);
        }

        private void Decimal_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!TryParsePrecio(tb.Text, out var v))
                tb.Text = "0.00";
            else
                tb.Text = v.ToString("0.00");
        }
        #endregion
    }
}