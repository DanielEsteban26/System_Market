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
        private readonly CompraService _compraService;
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _producto_service;

        private readonly List<DetalleCompra> _detalles = new();

        private List<Producto> _productos = new();
        private ICollectionView? _vistaProductos;

        private const string Simbolo = "S/";
        private static readonly Regex RegexEntero = new(@"^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex RegexDecimal = new(@"^[0-9\.,]+$", RegexOptions.Compiled);

        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(3));

        private DateTime _lastScanHandled = DateTime.MinValue;
        private string? _ultimoCodigoEscaneado; // para prellenar al crear producto

        // --- Nuevos miembros para detección de escaneo rápido ---
        private readonly StringBuilder _scanBuffer = new();
        private readonly DispatcherTimer _scanTimer;
        private DateTime _lastKeystroke = DateTime.MinValue;
        private readonly int _scanThresholdMs = 80; // umbral entre teclas para considerar "scan"

        // apoyo para captura cuando el primer carácter fue escrito en otra TextBox
        private TextBox? _scanSourceTextBox;
        private int _scanSourceOriginalLength;

        public CompraWindow()
        {
            InitializeComponent();

            snackbar.MessageQueue = _snackbarQueue;

            string conn = DatabaseInitializer.GetConnectionString();
            _compraService = new CompraService(conn);
            _proveedorService = new ProveedorService(conn);
            _producto_service = new ProductoService(conn);

            CargarCombos();

            // Inicializar timer que detecta final de secuencia rápida (escáner)
            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _scanTimer.Tick += ScanTimer_Tick;

            // Registrar interceptores globales para evitar que el escáner escriba en cantidad
            this.PreviewTextInput += Global_PreviewTextInput;
            this.PreviewKeyDown += Global_PreviewKeyDown;

            // Registra handlers para validar/formatear precios (permite sólo números y separador decimal)
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
                // Si controles no están presentes por alguna razón, no romper la ventana.
            }
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

        // Interceptor global de texto: decide si es escaneo rápido y lo captura
        private void Global_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            var focused = Keyboard.FocusedElement as FrameworkElement;
            var now = DateTime.UtcNow;
            var delta = (now - _lastKeystroke).TotalMilliseconds;

            bool bufferEmpty = _scanBuffer.Length == 0;
            bool isFast = delta < _scanThresholdMs;

            // Si no hay buffer todavía: iniciarlo y recordar la fuente (no consumimos la primera tecla)
            if (bufferEmpty)
            {
                _scanSourceTextBox = focused as TextBox;
                _scanSourceOriginalLength = _scanSourceTextBox?.Text.Length ?? 0;

                _scanBuffer.Append(e.Text);
                _scanTimer.Stop();
                _scanTimer.Start();
                _lastKeystroke = now;

                // No consumimos la primera tecla: permitimos escritura en el control (evita retrasos)
                return;
            }

            // Si ya existe buffer, sólo continuamos si la entrada es rápida (es un escaneo)
            if (!isFast)
            {
                // Entrada lenta => probable tecleo manual. Abortar modo scan y no interferir.
                _scanBuffer.Clear();
                _scanTimer.Stop();
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
                return;
            }

            // Secuencia rápida: seguir bufferizando y consumir para que no vaya a otros controles.
            _scanBuffer.Append(e.Text);
            _scanTimer.Stop();
            _scanTimer.Start();
            _lastKeystroke = now;
            e.Handled = true;
        }

        // Interceptor global de teclas: si ENTER y hay buffer procesar de inmediato
        private void Global_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Si hay buffer acumulado, usarlo como código
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

        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            _scanTimer.Stop();

            var bufferCode = _scanBuffer.ToString();
            _scanBuffer.Clear();

            // Si antes del scan el primer carácter fue escrito en alguna TextBox, leer lo añadido
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
                    // no modificamos aún el TextBox; solo examinamos
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                // reset source tracking
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
            }

            var code = (prefixFromControl + bufferCode).Trim();
            if (string.IsNullOrWhiteSpace(code)) return;

            // Sólo procesar si parece un barcode para evitar interferir con escritura normal
            if (IsLikelyBarcode(code))
            {
                // Si había texto extra en la TextBox origen, eliminarlo (consumido por el scan).
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
                catch { /* ignore */ }

                HandleScannedCode(code);

                // además limpiar textbox editable si existe
                try
                {
                    if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox tb)
                    {
                        tb.Clear();
                    }
                }
                catch { }
            }
            else
            {
                // No es barcode: no hacemos nada (dejamos la entrada manual intacta)
            }
        }

        // Mejora de la heurística: exigir longitud mínima para evitar falsos positivos
        private static bool IsLikelyBarcode(string code)
        {
            code = code.Trim();
            if (code.Length < 6) return false; // barcode típicos son más largos
            int digits = code.Count(char.IsDigit);
            // exigir al menos la mitad de caracteres como dígitos (ajusta si tu código es alfanumérico)
            return digits >= Math.Max(1, code.Length / 2);
        }

        private void cmbProducto_Loaded(object sender, RoutedEventArgs e)
        {
            // si el template tiene el EditableTextBox
            if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox tb)
            {
                tb.TextChanged += CmbProducto_TextChanged;
                tb.KeyDown += EditableTextBox_KeyDown; // maneja Enter (escáner)
                tb.PreviewTextInput += Editable_PreviewTextInput; // captura chars rápidos (escáner) para combo
            }
        }

        private void Editable_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            // Si el editable del combo recibe texto, bufferizamos y reiniciamos timer.
            if (string.IsNullOrEmpty(e.Text)) return;
            _scanBuffer.Append(e.Text);
            _scanTimer.Stop();
            _scanTimer.Start();
            // No marcamos Handled aquí: permitimos que el combo muestre el texto también.
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
                // CORRECCIÓN: usar existente.PrecioUnitario en lugar de una variable inexistente
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

            // Limpiar UI como antes
            cmbProducto.SelectedIndex = -1;
            cmbProducto.Text = string.Empty;
            lblProductoNombre.Text = "";
            txtCantidad.Clear();
            txtPrecioUnitario.Clear();
            OcultarEditorPrecios();

            // --- NUEVO: limpiar estado del buffer y devolver foco al combo editable ---
            try
            {
                _scanBuffer.Clear();
                _scanTimer.Stop();
                _scanSourceTextBox = null;
                _scanSourceOriginalLength = 0;
                // Reiniciar marca de tiempo para evitar lecturas rápidas residuales
                _lastKeystroke = DateTime.MinValue;

                // Intentar fijar foco de forma síncrona y asegurar con BeginInvoke
                if (cmbProducto.Template.FindName("PART_EditableTextBox", cmbProducto) is TextBox editableTb)
                {
                    // focus inmediato
                    editableTb.Focus();
                    // limpiar visualmente (no obligatorio)
                    editableTb.Clear();

                    // Asegurar con un invoke diferido que el control está listo para el siguiente scan
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
            catch
            {
                // no romper la UI si algo falla aquí
            }
        }

        private void RefrescarDetalle()
        {
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = _detalles;
        }

        private void CalcularTotal()
        {
            decimal total = _detalles.Sum(d => d.Subtotal);
            txtTotal.Text = CurrencyService.FormatSoles(total, "N2");
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

            // Usar el usuario actualmente logueado (sesión global)
            if (System_Market.Models.SesionActual.Usuario == null)
            {
                MessageBox.Show("No hay usuario logueado. Vuelva a iniciar sesión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Construir resumen para confirmación previa
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

        private static bool TryParsePrecio(string? texto, out decimal valor)
        {
            return CurrencyService.TryParseSoles(texto, out valor);
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
                tb.Text = CurrencyService.FormatSoles(0m, "N2");
            else
                tb.Text = CurrencyService.FormatSoles(v, "N2");
        }

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

        private void MostrarToast(string mensaje) => _snackbarQueue.Enqueue(mensaje);

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

        private void _producto_service_agregar_safe(Producto p)
        {
            try { _producto_service.AgregarProducto(p); } catch { }
        }

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