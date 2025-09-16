using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using MaterialDesignThemes.Wpf; // Snackbar
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class CompraWindow : Window
    {
        private readonly CompraService _compraService;
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _productoService;

        private readonly List<DetalleCompra> _detalles = new();

        private List<Producto> _productos = new();
        private ICollectionView? _vistaProductos;

        private const string Simbolo = "S/";
        private static readonly Regex RegexEntero = new(@"^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex RegexDecimal = new(@"^[0-9\.,]+$", RegexOptions.Compiled);

        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(3));

        private DateTime _lastScanHandled = DateTime.MinValue;
        private string? _ultimoCodigoEscaneado; // para prellenar al crear producto

        public CompraWindow()
        {
            InitializeComponent();

            snackbar.MessageQueue = _snackbarQueue;

            string conn = DatabaseInitializer.GetConnectionString();
            _compraService = new CompraService(conn);
            _proveedorService = new ProveedorService(conn);
            _productoService = new ProductoService(conn);

            CargarCombos();
        }

        public CompraWindow(string codigoInicial) : this()
        {
            if (!string.IsNullOrWhiteSpace(codigoInicial))
                PreSeleccionarProductoPorCodigo(codigoInicial);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // No enfocamos el textbox de código (está oculto). El gancho del escáner funciona igual.
            this.Focus();
        }

        private void PreSeleccionarProductoPorCodigo(string codigo)
        {
            _ultimoCodigoEscaneado = codigo;

            var prod = _productoService.ObtenerPorCodigoBarras(codigo);
            if (prod != null)
            {
                cmbProducto.SelectedValue = prod.Id;
                txtPrecioUnitario.Text = prod.PrecioCompra.ToString("0.00");
                lblProductoNombre.Text = prod.Nombre;

                txtCantidad.Text = "1";
                txtCantidad.SelectAll();
                txtCantidad.Focus();
                _lastScanHandled = DateTime.UtcNow;
            }
            else
            {
                MostrarToast($"El producto con código {codigo} no existe. Usa 'Nuevo' para crearlo.");
                cmbProducto.SelectedIndex = -1;
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
                btnProductoNuevo.Focus();
            }
        }

        private void CargarCombos()
        {
            var proveedores = _proveedorService.ObtenerTodos();
            cmbProveedor.ItemsSource = proveedores;
            cmbProveedor.DisplayMemberPath = "Nombre";
            cmbProveedor.SelectedValuePath = "Id";

            _productos = _productoService.ObtenerTodos();
            _vistaProductos = System.Windows.Data.CollectionViewSource.GetDefaultView(_productos);
            _vistaProductos.Filter = null;

            cmbProducto.ItemsSource = _vistaProductos;
            cmbProducto.SelectedValuePath = "Id";
        }

        private void cmbProducto_Loaded(object sender, RoutedEventArgs e)
        {
            // si el template tiene el EditableTextBox
            if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox tb)
                tb.TextChanged += CmbProducto_TextChanged;
        }

        private void CmbProducto_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_vistaProductos == null) return;
            string txt = (sender as TextBox)?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(txt))
            {
                _vistaProductos.Filter = null;
            }
            else
            {
                _vistaProductos.Filter = o =>
                {
                    if (o is not Producto p) return false;
                    return p.Nombre.Contains(txt, StringComparison.OrdinalIgnoreCase)
                           || (p.CodigoBarras?.Contains(txt, StringComparison.OrdinalIgnoreCase) ?? false);
                };
            }

            _vistaProductos.Refresh();
            cmbProducto.IsDropDownOpen = true;
        }

        private void btnAgregarDetalle_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProducto.SelectedValue == null)
            {
                MessageBox.Show("Seleccione un producto.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int cantidad = 1;
            var txtCant = txtCantidad.Text.Trim();
            if (!string.IsNullOrEmpty(txtCant) && (!int.TryParse(txtCant, out cantidad) || cantidad <= 0))
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
            lblProductoNombre.Text = "";
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
                    UsuarioId = 1,
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
                cmbProducto.Text = producto.Nombre;
                lblProductoNombre.Text = producto.Nombre;
                txtPrecioUnitario.Text = producto.PrecioCompra.ToString("0.00");

                txtCantidad.Text = "1";
                txtCantidad.SelectAll();
            }
            else
            {
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
                OcultarEditorPrecios();
            }
        }

        private void btnProductoNuevo_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoEdicionWindow(
                DatabaseInitializer.GetConnectionString(),
                producto: null,
                codigoPrefill: _ultimoCodigoEscaneado,
                bloquearCodigo: false);

            if (win.ShowDialog() == true)
            {
                _productoService.AgregarProducto(win.Producto);

                // Recargar productos
                _productos = _productoService.ObtenerTodos();
                _vistaProductos = System.Windows.Data.CollectionViewSource.GetDefaultView(_productos);
                _vistaProductos.Filter = null;
                cmbProducto.ItemsSource = _vistaProductos;
                cmbProducto.SelectedValuePath = "Id";

                // Forzar selección por Id
                SeleccionarProductoPorId(win.Producto.Id);

                txtCantidad.Text = "1";
                txtCantidad.SelectAll();
                txtCantidad.Focus();
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
            txtNuevoPrecioCompra.Text = CurrencyService.FormatNumber(prodSel.PrecioCompra, "N2");
            txtNuevoPrecioVenta.Text = CurrencyService.FormatNumber(prodSel.PrecioVenta, "N2");
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

        private void OnCantidadPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if ((DateTime.UtcNow - _lastScanHandled).TotalMilliseconds < 250)
            {
                e.Handled = true;
                return;
            }
            e.Handled = !RegexEntero.IsMatch(e.Text);
        }

        private void OnCantidadPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((DateTime.UtcNow - _lastScanHandled).TotalMilliseconds < 250)
            {
                e.Handled = true;
                return;
            }
        }

        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !RegexDecimal.IsMatch(e.Text);

        private void Decimal_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!TryParsePrecio(tb.Text, out var v))
                tb.Text = "0.00";
            else
                tb.Text = v.ToString("0.00");
        }

        // Recibido desde el hook de escáner
        public void HandleScannedCode(string codigo)
        {
            _ultimoCodigoEscaneado = codigo;
            var prod = _productoService.ObtenerPorCodigoBarras(codigo);
            if (prod == null)
            {
                MostrarToast($"El producto con código {codigo} no existe. Usa 'Nuevo' para crearlo.");
                cmbProducto.SelectedIndex = -1;
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
                btnProductoNuevo.Focus();
                return;
            }

            SeleccionarProductoPorId(prod.Id);
            txtCantidad.Text = "1";
            txtCantidad.SelectAll();
            txtCantidad.Focus();
            _lastScanHandled = DateTime.UtcNow;
        }

        // Si algún día usas ingreso manual (el control está oculto), esto sigue funcionando.
        private void TxtCodigoCompra_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            var code = txtCodigoCompra.Text.Trim();
            if (string.IsNullOrEmpty(code)) return;

            _ultimoCodigoEscaneado = code;

            var prod = _productoService.ObtenerPorCodigoBarras(code);
            if (prod != null)
            {
                cmbProducto.SelectedValue = prod.Id;
                lblProductoNombre.Text = prod.Nombre;
                txtPrecioUnitario.Text = prod.PrecioCompra.ToString("0.00");

                txtCantidad.Text = "1";
                txtCantidad.SelectAll();
                txtCantidad.Focus();
                _lastScanHandled = DateTime.UtcNow;
            }
            else
            {
                MostrarToast($"El producto con código {code} no existe. Usa 'Nuevo' para crearlo.");
                cmbProducto.SelectedIndex = -1;
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
                btnProductoNuevo.Focus();
            }
        }

        private void MostrarToast(string mensaje) => _snackbarQueue.Enqueue(mensaje);

        private void SeleccionarProductoPorId(int productoId)
        {
            // Busca en la lista actual
            var prod = _productos.FirstOrDefault(p => p.Id == productoId);
            if (prod != null)
            {
                // Establece SelectedItem directamente
                cmbProducto.SelectedItem = prod;
                // Redundancia: también SelectedValue
                cmbProducto.SelectedValue = prod.Id;

                lblProductoNombre.Text = prod.Nombre;
                txtPrecioUnitario.Text = prod.PrecioCompra.ToString("0.00");

                // Valor por defecto cantidad
                if (string.IsNullOrWhiteSpace(txtCantidad.Text))
                    txtCantidad.Text = "1";
            }
        }
    }
}