using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf; // Snackbar
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class CompraWindow : Window
    {
        // Servicios para operaciones de compra, proveedor y producto
        private readonly CompraService _compraService;
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _producto_service;

        // Lista de detalles de la compra actual (productos agregados)
        private readonly List<DetalleCompra> _detalles = new();

        // Lista de productos y vista filtrable para el ComboBox
        private List<Producto> _productos = new();
        private ICollectionView? _vistaProductos;

        // Constantes y expresiones regulares para validación de datos
        private const string Simbolo = "S/";
        private static readonly Regex RegexEntero = new(@"^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex RegexDecimal = new(@"^[0-9\.,]+$", RegexOptions.Compiled);

        // Cola de mensajes para mostrar notificaciones tipo Snackbar
        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(3));

        // Variables para control de escaneo de códigos de barras
        private DateTime _lastScanHandled = DateTime.MinValue;
        private string? _ultimoCodigoEscaneado; // Último código escaneado

        // Buffer y timer para detectar secuencias rápidas de teclado (escáner)
        private readonly StringBuilder _scanBuffer = new();
        private readonly DispatcherTimer _scanTimer;
        private DateTime _lastKeystroke = DateTime.MinValue;
        private readonly int _scanThresholdMs = 80; // ms entre teclas para considerar escaneo

        // Soporte para capturar el primer carácter si se escribió en un TextBox
        private TextBox? _scanSourceTextBox;
        private int _scanSourceOriginalLength;

        public CompraWindow()
        {
            InitializeComponent();

            snackbar.MessageQueue = _snackbarQueue;

            // Inicialización de servicios con la cadena de conexión
            string conn = DatabaseInitializer.GetConnectionString();
            _compraService = new CompraService(conn);
            _proveedorService = new ProveedorService(conn);
            _producto_service = new ProductoService(conn);

            CargarCombos();

            // Configuración del timer para detectar fin de escaneo rápido
            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _scanTimer.Tick += ScanTimer_Tick;

            // Interceptores globales para capturar escaneos y evitar escritura accidental
            this.PreviewTextInput += Global_PreviewTextInput;
            this.PreviewKeyDown += Global_PreviewKeyDown;

            // Handlers para validar y formatear precios en los TextBox correspondientes
            try
            {
                txtPrecioUnitario.PreviewTextInput += Decimal_PreviewTextInput;
                txtPrecioUnitario.LostFocus += Decimal_LostFocus;
                DataObject.AddPastingHandler(txtPrecioUnitario, Precio_PasteHandler);

                txtNuevoPrecioCompra.PreviewTextInput += Decimal_PreviewTextInput;
                txtNuevoPrecioCompra.LostFocus += Decimal_LostFocus;
                DataObject.AddPastingHandler(txtNuevoPrecioCompra, Precio_PasteHandler);

                txtNuevoPrecioVenta.PreviewTextInput += Decimal_PreviewTextInput;
                txtNuevoPrecioVenta.LostFocus += Decimal_LostFocus;
                DataObject.AddPastingHandler(txtNuevoPrecioVenta, Precio_PasteHandler);
            }
            catch
            {
                // Si los controles no existen, no se lanza excepción
            }
        }

        // Permite abrir la ventana con un producto preseleccionado por código de barras
        public CompraWindow(string codigoInicial) : this()
        {
            if (!string.IsNullOrWhiteSpace(codigoInicial))
                PreSeleccionarProductoPorCodigo(codigoInicial);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Se asegura que la ventana tenga el foco al cargar
            this.Focus();
        }

        // Busca y selecciona un producto por código de barras al abrir la ventana.
        // Si no existe, muestra un mensaje y limpia los campos.
        private void PreSeleccionarProductoPorCodigo(string codigo)
        {
            _ultimoCodigoEscaneado = codigo;

            var prod = _producto_service.ObtenerPorCodigoBarras(codigo);
            if (prod != null)
            {
                cmbProducto.SelectedValue = prod.Id;
                txtPrecioUnitario.Text = CurrencyService.FormatSoles(prod.PrecioCompra, "N2");
                lblProductoNombre.Text = prod.Nombre;

                txtCantidad.Text = "1";
                txtCantidad.SelectAll();
                txtCantidad.Focus();
                _lastScanHandled = DateTime.UtcNow;
            }
            else
            {
                MostrarToast($"Código {codigo} no existe. Cree el producto.");
                cmbProducto.SelectedIndex = -1;
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
            }
        }

        // Carga proveedores y productos en los ComboBox de la ventana.
        private void CargarCombos()
        {
            var proveedores = _proveedorService.ObtenerTodos();
            cmbProveedor.ItemsSource = proveedores;
            cmbProveedor.DisplayMemberPath = "Nombre";
            cmbProveedor.SelectedValuePath = "Id";

            _productos = _producto_service.ObtenerTodos();
            _vistaProductos = System.Windows.Data.CollectionViewSource.GetDefaultView(_productos);
            _vistaProductos.Filter = null;

            cmbProducto.ItemsSource = _vistaProductos;
            cmbProducto.SelectedValuePath = "Id";
        }

        // Intercepta la entrada de texto para detectar escaneo rápido de código de barras.
        // Si la secuencia es rápida, la bufferiza y evita que se escriba en el control.
        private void Global_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            var focused = Keyboard.FocusedElement as FrameworkElement;
            var now = DateTime.UtcNow;
            var delta = (now - _lastKeystroke).TotalMilliseconds;

            bool bufferEmpty = _scanBuffer.Length == 0;
            bool isFast = delta < _scanThresholdMs;

            if (bufferEmpty)
            {
                // Inicia el buffer y guarda la fuente si es el primer carácter
                _scanSourceTextBox = focused as TextBox;
                _scanSourceOriginalLength = _scanSourceTextBox?.Text.Length ?? 0;

                _scanBuffer.Append(e.Text);
                _scanTimer.Stop();
                _scanTimer.Start();
                _lastKeystroke = now;
                return;
            }

            if (!isFast)
            {
                // Si la entrada es lenta, se asume tecleo manual y se descarta el buffer
                _scanBuffer.Clear();
                _scanTimer.Stop();
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
                return;
            }

            // Si es una secuencia rápida, se considera escaneo y se consume el carácter
            _scanBuffer.Append(e.Text);
            _scanTimer.Stop();
            _scanTimer.Start();
            _lastKeystroke = now;
            e.Handled = true;
        }

        // Intercepta la tecla Enter para procesar el buffer de escaneo inmediatamente.
        private void Global_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_scanBuffer.Length > 0)
                {
                    var code = _scanBuffer.ToString();
                    _scanBuffer.Clear();
                    _scanTimer.Stop();
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        HandleScannedCode(code);
                        e.Handled = true;
                    }
                }
            }
        }

        // Timer para finalizar la captura de un escaneo rápido.
        // Si el texto parece un código de barras, lo procesa.
        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            _scanTimer.Stop();

            var bufferCode = _scanBuffer.ToString();
            _scanBuffer.Clear();

            // Si el primer carácter fue escrito en un TextBox, lo recupera
            string prefixFromControl = string.Empty;
            try
            {
                if (_scanSourceTextBox != null)
                {
                    var current = _scanSourceTextBox.Text ?? string.Empty;
                    if (current.Length > _scanSourceOriginalLength)
                    {
                        prefixFromControl = current.Substring(_scanSourceOriginalLength);
                    }
                }
            }
            catch { }
            finally
            {
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
            }

            var code = (prefixFromControl + bufferCode).Trim();
            if (string.IsNullOrWhiteSpace(code)) return;

            // Solo procesa si parece un código de barras válido
            if (IsLikelyBarcode(code))
            {
                try
                {
                    if (!string.IsNullOrEmpty(prefixFromControl) && _scanSourceTextBox is not null)
                    {
                        var current = _scanSourceTextBox.Text ?? string.Empty;
                        if (current.Length >= prefixFromControl.Length)
                        {
                            var newText = current.Substring(0, current.Length - prefixFromControl.Length);
                            _scanSourceTextBox.Text = newText;
                        }
                    }
                }
                catch { }

                HandleScannedCode(code);

                // Limpia el TextBox editable del ComboBox si existe
                try
                {
                    if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox tb)
                    {
                        tb.Clear();
                    }
                }
                catch { }
            }
        }

        // Heurística para determinar si un texto es probablemente un código de barras.
        private static bool IsLikelyBarcode(string code)
        {
            code = code.Trim();
            if (code.Length < 6) return false;
            int digits = code.Count(char.IsDigit);
            return digits >= Math.Max(1, code.Length / 2);
        }

        // Asocia eventos al TextBox editable del ComboBox de productos.
        private void cmbProducto_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox tb)
            {
                tb.TextChanged += CmbProducto_TextChanged;
                tb.KeyDown += EditableTextBox_KeyDown;
                tb.PreviewTextInput += Editable_PreviewTextInput;
            }
        }

        // Bufferiza caracteres para escaneo en el ComboBox editable.
        private void Editable_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            _scanBuffer.Append(e.Text);
            _scanTimer.Stop();
            _scanTimer.Start();
        }

        // Filtra la lista de productos según el texto ingresado en el ComboBox.
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

        // Agrega un producto y su cantidad a la lista de detalles de la compra.
        // Si el producto ya existe con el mismo precio, suma la cantidad.
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
                // Si ya existe el producto con ese precio, solo suma la cantidad
                existente.Cantidad += cantidad;
                existente.Subtotal = existente.Cantidad * existente.PrecioUnitario;
            }
            else
            {
                // Si es nuevo, lo agrega a la lista de detalles
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

            // Limpia los campos de la UI para el siguiente ingreso
            cmbProducto.SelectedIndex = -1;
            cmbProducto.Text = string.Empty;
            lblProductoNombre.Text = "";
            txtCantidad.Clear();
            txtPrecioUnitario.Clear();
            OcultarEditorPrecios();

            // Limpia el buffer de escaneo y devuelve el foco al ComboBox editable
            try
            {
                _scanBuffer.Clear();
                _scanTimer.Stop();
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
                _lastKeystroke = DateTime.MinValue;

                if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox editableTb)
                {
                    editableTb.Focus();
                    editableTb.Clear();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            editableTb.Focus();
                            editableTb.SelectAll();
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch { }
        }

        // Refresca la visualización de los detalles de la compra en el DataGrid.
        private void RefrescarDetalle()
        {
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = _detalles;
        }

        // Calcula el total de la compra sumando los subtotales de los detalles.
        private void CalcularTotal()
        {
            decimal total = _detalles.Sum(d => d.Subtotal);
            txtTotal.Text = CurrencyService.FormatSoles(total, "N2");
        }

        // Valida y registra la compra en la base de datos.
        // Muestra un resumen para confirmación antes de registrar.
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

            if (System_Market.Models.SesionActual.Usuario == null)
            {
                MessageBox.Show("No hay usuario logueado. Vuelva a iniciar sesión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Muestra un resumen para confirmación antes de registrar
            var sb = new StringBuilder();
            sb.AppendLine("Confirme la compra con los siguientes productos:");
            sb.AppendLine();
            foreach (var d in _detalles)
            {
                var precioFmt = CurrencyService.FormatSoles(d.PrecioUnitario, "N2");
                var subFmt = CurrencyService.FormatSoles(d.Subtotal, "N2");
                sb.AppendLine($"{d.ProductoNombre} | Cant: {d.Cantidad} | Precio: {precioFmt} | Subtotal: {subFmt}");
            }
            sb.AppendLine();
            decimal total = _detalles.Sum(d => d.Subtotal);
            sb.AppendLine($"Total: {CurrencyService.FormatSoles(total, "N2")}");
            sb.AppendLine();
            sb.AppendLine("¿Está todo correcto?");

            var confirmar = MessageBox.Show(sb.ToString(), "Confirmar compra", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes)
                return;

            try
            {
                var usuarioActual = System_Market.Models.SesionActual.Usuario;

                var compra = new Compra
                {
                    UsuarioId = usuarioActual.Id,
                    UsuarioNombre = usuarioActual.Nombre,
                    ProveedorId = Convert.ToInt32(cmbProveedor.SelectedValue),
                    Fecha = DateTime.Now,
                    Total = total,
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

        // Abre la ventana de historial de compras.
        private void btnHistorialCompras_Click(object sender, RoutedEventArgs e)
        {
            var historial = new HistorialComprasWindow { Owner = this };
            historial.ShowDialog();
        }

        // Cuando se selecciona un producto, actualiza los campos relacionados.
        private void cmbProducto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var producto = cmbProducto.SelectedItem as Producto;
            if (producto != null)
            {
                cmbProducto.Text = producto.Nombre;
                lblProductoNombre.Text = producto.Nombre;
                txtPrecioUnitario.Text = CurrencyService.FormatSoles(producto.PrecioCompra, "N2");

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

        // btnProductoNuevo_Click eliminado

        // Muestra el panel para editar precios de compra y venta del producto seleccionado.
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
            txtNuevoPrecioCompra.Text = CurrencyService.FormatSoles(prodSel.PrecioCompra, "N2");
            txtNuevoPrecioVenta.Text = CurrencyService.FormatSoles(prodSel.PrecioVenta, "N2");
            panelEditorPrecios.Visibility = Visibility.Visible;
        }

        // Guarda los nuevos precios de compra y venta del producto seleccionado.
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
                _producto_service.ActualizarProducto(prodSel);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo actualizar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtPrecioUnitario.Text = CurrencyService.FormatSoles(prodSel.PrecioCompra, "N2");
            MessageBox.Show("Precios actualizados.", "OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
            OcultarEditorPrecios();
        }

        private void BtnCancelarPrecios_Click(object sender, RoutedEventArgs e) => OcultarEditorPrecios();

        private void OcultarEditorPrecios() => panelEditorPrecios.Visibility = Visibility.Collapsed;

        // Intenta convertir un texto a decimal usando el servicio de moneda.
        private static bool TryParsePrecio(string? texto, out decimal valor)
        {
            return CurrencyService.TryParseSoles(texto, out valor);
        }

        // Valida que solo se ingresen números enteros en la cantidad.
        private void OnCantidadPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if ((DateTime.UtcNow - _lastScanHandled).TotalMilliseconds < 250)
            {
                e.Handled = true;
                return;
            }
            e.Handled = !RegexEntero.IsMatch(e.Text);
        }

        // Evita la edición de cantidad inmediatamente después de un escaneo.
        private void OnCantidadPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((DateTime.UtcNow - _lastScanHandled).TotalMilliseconds < 250)
            {
                e.Handled = true;
                return;
            }
        }

        // Valida que solo se ingresen números decimales en los campos de precio.
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !RegexDecimal.IsMatch(e.Text);

        // Formatea el valor del TextBox de precio al perder el foco.
        private void Decimal_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!TryParsePrecio(tb.Text, out var v))
                tb.Text = CurrencyService.FormatSoles(0m, "N2");
            else
                tb.Text = CurrencyService.FormatSoles(v, "N2");
        }

        // Valida el pegado de texto en los campos de precio.
        private void Precio_PasteHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }
            var txt = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!;
            if (!RegexDecimal.IsMatch(txt) && !CurrencyService.TryParseSoles(txt, out _))
                e.CancelCommand();
        }

        // Procesa un código escaneado: selecciona el producto y actualiza la UI.
        public void HandleScannedCode(string codigo)
        {
            _ultimoCodigoEscaneado = codigo;

            try
            {
                cmbProducto.SelectedIndex = -1;
                cmbProducto.Text = string.Empty;
                cmbProducto.IsDropDownOpen = false;
            }
            catch { }

            var prodFromService = _producto_service.ObtenerPorCodigoBarras(codigo);
            if (prodFromService == null)
            {
                MostrarToast($"Código {codigo} no existe. Cree el producto.");
                cmbProducto.SelectedIndex = -1;
                lblProductoNombre.Text = "";
                txtPrecioUnitario.Clear();
                return;
            }

            var localInstance = _productos.FirstOrDefault(p => p.Id == prodFromService.Id) ?? prodFromService;

            cmbProducto.SelectedItem = localInstance;
            cmbProducto.SelectedValue = localInstance.Id;
            cmbProducto.Text = localInstance.Nombre;

            lblProductoNombre.Text = localInstance.Nombre;
            txtPrecioUnitario.Text = CurrencyService.FormatSoles(localInstance.PrecioCompra, "N2");

            txtCantidad.Text = "1";
            txtCantidad.SelectAll();
            txtCantidad.Focus();

            _lastScanHandled = DateTime.UtcNow;
        }

        // Muestra un mensaje tipo Snackbar en la parte inferior de la ventana.
        private void MostrarToast(string mensaje) => _snackbarQueue.Enqueue(mensaje);

        // Selecciona un producto por su Id y actualiza los campos relacionados.
        private void SeleccionarProductoPorId(int productoId)
        {
            var prod = _productos.FirstOrDefault(p => p.Id == productoId);
            if (prod != null)
            {
                cmbProducto.SelectedItem = prod;
                cmbProducto.SelectedValue = prod.Id;

                lblProductoNombre.Text = prod.Nombre;
                txtPrecioUnitario.Text = CurrencyService.FormatSoles(prod.PrecioCompra, "N2");

                if (string.IsNullOrWhiteSpace(txtCantidad.Text))
                    txtCantidad.Text = "1";
            }
        }

        // Agrega un producto de forma segura (ignora excepciones).
        private void _producto_service_agregar_safe(Producto p)
        {
            try { _producto_service.AgregarProducto(p); } catch { }
        }

        // Refresca la lista de productos y proveedores, y actualiza la UI.
        private void btnRefrescar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CargarCombos();
                _productos = _producto_service.ObtenerTodos();
                _vistaProductos = System.Windows.Data.CollectionViewSource.GetDefaultView(_productos);
                _vistaProductos.Filter = null;
                cmbProducto.ItemsSource = _vistaProductos;

                RefrescarDetalle();
                CalcularTotal();

                try { _snackbarQueue.Enqueue("Datos actualizados"); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al refrescar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Elimina un detalle de la lista de compra tras confirmación del usuario.
        private void EliminarDetalle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DetalleCompra detalle)
            {
                var msg = $"¿Eliminar '{detalle.ProductoNombre}' de la lista?";
                if (MessageBox.Show(msg, "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                _detalles.Remove(detalle);
                RefrescarDetalle();
                CalcularTotal();
            }
        }

        // Permite procesar un código ingresado manualmente en el ComboBox editable al presionar Enter.
        private void EditableTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (sender is not TextBox tb) return;

            var code = tb.Text?.Trim();
            if (string.IsNullOrEmpty(code)) return;

            HandleScannedCode(code);

            _scanBuffer.Clear();
            _scanTimer.Stop();
            tb.Clear();

            e.Handled = true;
        }

        // Permite procesar un código ingresado en el TextBox de código de compra al presionar Enter.
        private void TxtCodigoCompra_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var code = txtCodigoCompra.Text?.Trim();
            if (string.IsNullOrEmpty(code)) return;

            // Procesar como si viniera del escáner
            HandleScannedCode(code);

            // Limpiar y marcar como manejado
            txtCodigoCompra.Clear();
            e.Handled = true;
        }
    }
}