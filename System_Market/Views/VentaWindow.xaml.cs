using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    // Ventana para registrar ventas: permite escanear productos, controlar stock y registrar la venta.
    // Incluye control de duplicados, cache de productos y bloqueo/desbloqueo de edición de código.
    public partial class VentaWindow : Window
    {
        private readonly ProductoService productoService;
        private readonly VentaService ventaService;
        private ObservableCollection<DetalleVenta> detalleVenta;

        // Id del usuario en sesión (por defecto 1 si no hay sesión activa)
        private int usuarioId;
        private bool _codigoBloqueado;

        private const int MinManualCodeLength = 3;
        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(2));

        private string? _ultimoCodigoEscaneado;
        private DateTime _ultimoCodigoEscaneadoTime = DateTime.MinValue;

        // Cache de productos por código de barras
        private Dictionary<string, Producto> _cacheProductosPorCodigo = new(StringComparer.OrdinalIgnoreCase);
        private bool _cacheListaLista;
        private readonly List<string> _codigosPendientes = new();

        private bool _creacionProductoEnCurso;

        public VentaWindow()
        {
            InitializeComponent();
            productoService = new ProductoService(DatabaseInitializer.GetConnectionString());
            ventaService = new VentaService(DatabaseInitializer.GetConnectionString());
            detalleVenta = new ObservableCollection<DetalleVenta>();
            dgDetalleVenta.ItemsSource = detalleVenta;
            ActualizarTotales();
            snackbar.MessageQueue = _snackbarQueue;
            BloquearEdicionCodigo();
            txtCodigoBarra.Clear();

            usuarioId = System_Market.Models.SesionActual.Usuario?.Id ?? 1;

            DrenarEscaneosPendientes();

            Loaded += VentaWindow_Loaded;
            Activated += VentaWindow_Activated;
        }

        public VentaWindow(string codigoInicial, bool bloquearCodigo = true) : this()
        {
            if (!string.IsNullOrWhiteSpace(codigoInicial))
            {
                _codigoBloqueado = bloquearCodigo;
                if (_codigoBloqueado) BloquearEdicionCodigo();
                AgregarProductoDesdeCodigo(codigoInicial.Trim(), mostrarMensajes: false);
                txtCodigoBarra.Clear();

                try
                {
                    _ultimoCodigoEscaneado = NormalizarCodigoParaBusqueda(codigoInicial);
                    _ultimoCodigoEscaneadoTime = DateTime.UtcNow;
                }
                catch { }
            }
        }

        // Drena códigos pendientes de escaneo (por ejemplo, si se escaneó sin ventana abierta)
        private async void DrenarEscaneosPendientes()
        {
            var list = BarcodeScannerService.DrainPendingVentaCodes();
            if (list.Count == 0) return;
            foreach (var c in list)
                HandleScannedCode(c);
        }

        private void VentaWindow_Activated(object? sender, EventArgs e)
        {
            DrenarEscaneosPendientes();
        }

        private async void VentaWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await CargarCacheProductosAsync();

            // Oculta el botón "Agregar producto" si existe en el XAML
            try
            {
                if (this.FindName("btnAgregarProducto") is Button btn)
                {
                    btn.Visibility = Visibility.Collapsed;
                }
            }
            catch { }

            BarcodeScannerService.TryReplayLastCodeFor(this, TimeSpan.FromSeconds(3));

            if (!_codigoBloqueado)
                txtCodigoBarra.Focus();

            if (_codigosPendientes.Count > 0)
            {
                foreach (var c in _codigosPendientes.ToList())
                    AgregarProductoDesdeCodigo(c, mostrarMensajes: false);
                _codigosPendientes.Clear();
            }
        }

        // Precarga productos en cache para búsquedas rápidas
        private async Task CargarCacheProductosAsync()
        {
            try
            {
                var lista = await Task.Run(productoService.ObtenerTodos);
                _cacheProductosPorCodigo = lista
                    .Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarras))
                    .GroupBy(p => p.CodigoBarras!.Trim())
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                _cacheListaLista = true;
            }
            catch (Exception ex)
            {
                _snackbarQueue.Enqueue("No se pudo precargar productos: " + ex.Message);
                _cacheListaLista = false;
            }
        }

        // Procesa un código escaneado, evitando duplicados rápidos
        public void HandleScannedCode(string codigo)
        {
            var normalizado = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(normalizado)) return;

            if (!string.IsNullOrEmpty(_ultimoCodigoEscaneado) &&
                string.Equals(_ultimoCodigoEscaneado, normalizado, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.UtcNow - _ultimoCodigoEscaneadoTime).TotalMilliseconds < 800)
            {
                return;
            }

            _ultimoCodigoEscaneado = normalizado;
            _ultimoCodigoEscaneadoTime = DateTime.UtcNow;

            txtCodigoBarra.Text = normalizado;
            txtCodigoBarra.CaretIndex = normalizado.Length;

            if (!_cacheListaLista)
            {
                if (_codigosPendientes.Count == 0 || !_codigosPendientes.Last().Equals(normalizado, StringComparison.OrdinalIgnoreCase))
                {
                    _codigosPendientes.Add(normalizado);
                    _snackbarQueue.Enqueue("Cargando productos... se añadirá.");
                }
            }
            else
            {
                AgregarProductoDesdeCodigo(normalizado, mostrarMensajes: false);
            }

            txtCodigoBarra.Clear();
        }

        // Busca un producto en cache o en BD por código de barras
        private Producto? BuscarProductoEnCacheODB(string codigoRaw)
        {
            var codigo = NormalizarCodigoParaBusqueda(codigoRaw);
            if (string.IsNullOrEmpty(codigo)) return null;

            if (_cacheListaLista && _cacheProductosPorCodigo.TryGetValue(codigo, out var pCache))
                return pCache;

            var p = productoService.ObtenerPorCodigoBarras(codigo);
            if (p != null && _cacheListaLista && !string.IsNullOrWhiteSpace(p.CodigoBarras))
                _cacheProductosPorCodigo[p.CodigoBarras.Trim()] = p;
            return p;
        }

        // Normaliza el código para búsqueda (quita espacios y caracteres de control)
        private static string NormalizarCodigoParaBusqueda(string? codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
            var trimmed = codigo.Trim();
            return new string(trimmed.Where(ch => !char.IsControl(ch)).ToArray());
        }

        // Agrega un producto a la venta desde un código, mostrando mensajes si corresponde
        private async void AgregarProductoDesdeCodigo(string codigo, bool mostrarMensajes)
        {
            codigo = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(codigo)) return;

            Producto? producto = BuscarProductoEnCacheODB(codigo);

            if (producto == null)
            {
                await Task.Delay(40);
                producto = BuscarProductoEnCacheODB(codigo);
            }

            if (producto == null)
                producto = RefrescarProductoDesdeBD(codigo);

            if (producto == null)
            {
                var todos = productoService.ObtenerTodos();
                foreach (var det in detalleVenta)
                {
                    var p = todos.FirstOrDefault(x => x.Id == det.ProductoId);
                    if (p != null && !string.IsNullOrWhiteSpace(p.CodigoBarras) &&
                        NormalizarCodigoParaBusqueda(p.CodigoBarras) == codigo)
                    {
                        producto = p;
                        break;
                    }
                }
            }

            if (producto == null)
            {
                var mensaje = $"Producto no encontrado para el código '{codigo}'.\n\n" +
                              "Sugerencia: vaya a 'Compras' o 'Productos' para crearlo o registrarlo antes de venderlo.";
                MessageBox.Show(mensaje, "Producto no encontrado", MessageBoxButton.OK, MessageBoxImage.Information);
                _snackbarQueue.Enqueue("Producto no encontrado. Abrir Compras o Productos para crearlo/registrarlo.");
                return;
            }

            AgregarOIncrementarProducto(producto, mostrarMensajes);
        }

        // Permite agregar producto manualmente con Enter si el código es suficientemente largo
        private void txtCodigoBarra_KeyDown(object sender, KeyEventArgs e)
        {
            if (_codigoBloqueado) return;
            if (e.Key == Key.Enter)
            {
                var manual = (txtCodigoBarra.Text ?? string.Empty).Trim();
                if (manual.Length < MinManualCodeLength) return;
                btnAgregarProducto_Click(sender, e);
            }
        }

        private void TxtCodigoBarra_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_codigoBloqueado) e.Handled = true;
        }

        private void TxtCodigoBarra_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_codigoBloqueado && EsTeclaDigito(e.Key)) e.Handled = true;
        }

        private static bool EsTeclaDigito(Key k) =>
            (k >= Key.D0 && k <= Key.D9 && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            || (k >= Key.NumPad0 && k <= Key.NumPad9);

        // Búsqueda robusta: cache + variantes BD
        private Producto? BuscarProductoRobusto(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;
            var norm = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(norm)) return null;
            return BuscarProductoEnCacheODB(norm) ?? RefrescarProductoDesdeBD(norm);
        }

        // Agrega producto al detalle o incrementa cantidad si ya existe
        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_creacionProductoEnCurso) return;

            var codeTyped = (txtCodigoBarra.Text ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(codeTyped))
            {
                MessageBox.Show("La creación/edición de productos desde la ventana de ventas está deshabilitada.\n\n" +
                                "Use el menú 'Productos' o 'Compras' para crear o registrar productos.", "Funcionalidad deshabilitada",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                if (!_codigoBloqueado)
                {
                    txtCodigoBarra.Clear();
                    txtCodigoBarra.Focus();
                }
                return;
            }

            var existente = BuscarProductoRobusto(codeTyped);

            if (existente != null)
            {
                AgregarOIncrementarProducto(existente, mostrarMensajes: false);
                _snackbarQueue.Enqueue($"Cantidad de '{existente.Nombre}' actualizada.");

                if (!_codigoBloqueado)
                {
                    txtCodigoBarra.Clear();
                    txtCodigoBarra.Focus();
                }
                return;
            }

            var mensajeNoExiste = $"Producto no encontrado para el código '{codeTyped}'.\n\n" +
                                  "Vaya a 'Compras' o 'Productos' para crearlo o registrarlo antes de intentar venderlo.";
            MessageBox.Show(mensajeNoExiste, "Producto no encontrado", MessageBoxButton.OK, MessageBoxImage.Information);
            _snackbarQueue.Enqueue("Producto no encontrado. Ir a Compras o Productos para crearlo.");

            if (!_codigoBloqueado)
            {
                txtCodigoBarra.Clear();
                txtCodigoBarra.Focus();
            }
        }

        // Aumenta la cantidad de un producto en el detalle de venta
        private void btnAumentarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is DetalleVenta det)
            {
                var producto = productoService.ObtenerTodos().FirstOrDefault(p => p.Id == det.ProductoId);
                if (producto == null)
                {
                    _snackbarQueue.Enqueue($"No encontrado (ID {det.ProductoId}).");
                    return;
                }

                int nuevaCantidad = det.Cantidad + 1;
                if (nuevaCantidad > producto.Stock)
                {
                    _snackbarQueue.Enqueue($"Stock insuficiente. Disp: {producto.Stock}");
                    return;
                }

                det.Cantidad = nuevaCantidad;
                det.Subtotal = det.Cantidad * det.PrecioUnitario;
                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

        // Disminuye la cantidad o elimina el producto del detalle de venta
        private void btnDisminuirCantidad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is DetalleVenta det)
            {
                if (det.Cantidad > 1)
                {
                    det.Cantidad--;
                    det.Subtotal = det.Cantidad * det.PrecioUnitario;
                }
                else
                {
                    detalleVenta.Remove(det);
                }
                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

        // Permite eliminar un producto del detalle con la tecla Delete
        private void dgDetalleVenta_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && dgDetalleVenta.SelectedItem is DetalleVenta seleccionado)
            {
                detalleVenta.Remove(seleccionado);
                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

        // Registra la venta en la base de datos tras validaciones y confirmación
        private void btnRegistrarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!detalleVenta.Any())
            {
                MessageBox.Show("Debe agregar al menos un producto.");
                return;
            }

            foreach (var det in detalleVenta)
            {
                var producto = productoService.ObtenerTodos().FirstOrDefault(p => p.Id == det.ProductoId);
                if (producto == null)
                {
                    MessageBox.Show($"Producto no encontrado (ID: {det.ProductoId}).");
                    return;
                }
                if (det.Cantidad > producto.Stock)
                {
                    MessageBox.Show($"Stock insuficiente para '{producto.Nombre}'. Disp: {producto.Stock}, solicitado: {det.Cantidad}",
                        "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Confirme la venta con los siguientes productos:");
            sb.AppendLine();
            foreach (var d in detalleVenta)
            {
                sb.AppendLine($"{d.ProductoNombre} | Cant: {d.Cantidad} | Precio: {d.PrecioUnitario:C} | Subtotal: {d.Subtotal:C}");
            }
            sb.AppendLine();
            decimal total = detalleVenta.Sum(d => d.Subtotal);
            sb.AppendLine($"Total: {total:C}");
            sb.AppendLine();
            sb.AppendLine("¿Está todo correcto?");

            var confirmar = MessageBox.Show(sb.ToString(), "Confirmar venta", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes)
                return;

            try
            {
                var venta = new Venta
                {
                    UsuarioId = System_Market.Models.SesionActual.Usuario?.Id ?? usuarioId,
                    Fecha = DateTime.Now,
                    Estado = "Activa"
                };

                int ventaId = ventaService.AgregarVentaConDetalles(venta, detalleVenta.ToList());

                MessageBox.Show($"Venta registrada correctamente. Id: {ventaId}", "Venta", MessageBoxButton.OK, MessageBoxImage.Information);

                detalleVenta.Clear();
                ActualizarTotales();

                if (!_codigoBloqueado)
                    txtCodigoBarra.Focus();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar: " + ex.Message);
            }
        }

        // Actualiza el total mostrado en la interfaz
        private void ActualizarTotales()
        {
            decimal total = detalleVenta.Sum(d => d.Subtotal);
            txtTotal.Text = $"Total: {total:C}";
        }

        // Abre la ventana de historial de ventas
        private void BtnHistorial_Click(object sender, RoutedEventArgs e)
        {
            var historial = new HistorialVentasWindow();
            historial.ShowDialog();
        }

        // Permite desbloquear el campo de código para edición manual
        private void BtnDesbloquearCodigo_Click(object sender, RoutedEventArgs e) => DesbloquearEdicionCodigo();

        // Bloquea la edición manual del campo de código de barras
        private void BloquearEdicionCodigo()
        {
            _codigoBloqueado = true;
            txtCodigoBarra.IsReadOnly = true;
            txtCodigoBarra.IsHitTestVisible = false;
            txtCodigoBarra.Focusable = false;
            txtCodigoBarra.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            txtCodigoBarra.Cursor = Cursors.Arrow;
            txtCodigoBarra.ToolTip = "F2 o 'Editar' para escribir un código manual.";
        }

        // Desbloquea la edición manual del campo de código de barras
        private void DesbloquearEdicionCodigo()
        {
            _codigoBloqueado = false;
            txtCodigoBarra.IsReadOnly = false;
            txtCodigoBarra.IsHitTestVisible = true;
            txtCodigoBarra.Focusable = true;
            txtCodigoBarra.ClearValue(TextBox.BackgroundProperty);
            txtCodigoBarra.Cursor = Cursors.IBeam;
            txtCodigoBarra.ToolTip = null;
            txtCodigoBarra.Focus();
            txtCodigoBarra.SelectAll();
        }

        // Permite desbloquear el campo de código con F2
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                DesbloquearEdicionCodigo();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        // Permite crear un producto desde la ventana de ventas (no se usa por defecto)
        private Producto? CrearProductoInteractivo(string? codigoPrefill, bool bloquearCodigo = true)
        {
            if (_creacionProductoEnCurso) return null;
            _creacionProductoEnCurso = true;

            try
            {
                var win = new ProductoEdicionWindow(
                    DatabaseInitializer.GetConnectionString(),
                    producto: null,
                    codigoPrefill: string.IsNullOrWhiteSpace(codigoPrefill) ? null : codigoPrefill.Trim(),
                    bloquearCodigo: bloquearCodigo && !string.IsNullOrWhiteSpace(codigoPrefill))
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var ok = win.ShowDialog() == true && win.Producto != null;
                if (!ok) return null;

                try
                {
                    productoService.AgregarProducto(win.Producto);

                    Producto? recien = null;
                    if (!string.IsNullOrWhiteSpace(win.Producto.CodigoBarras))
                        recien = productoService.ObtenerPorCodigoBarras(win.Producto.CodigoBarras!);

                    if (recien == null)
                    {
                        recien = productoService.ObtenerTodos()
                            .FirstOrDefault(p => p.Nombre.Equals(win.Producto.Nombre, StringComparison.OrdinalIgnoreCase));
                    }

                    if (recien == null)
                    {
                        _snackbarQueue.Enqueue("No se pudo recuperar el nuevo producto.");
                        return null;
                    }

                    if (_cacheListaLista && !string.IsNullOrWhiteSpace(recien.CodigoBarras))
                        _cacheProductosPorCodigo[recien.CodigoBarras] = recien;

                    return recien;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al guardar nuevo producto: " + ex.Message,
                        "Producto", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            finally
            {
                _creacionProductoEnCurso = false;
            }
        }

        // Agrega o incrementa la cantidad de un producto en la venta
        private void AgregarOIncrementarProducto(Producto producto, bool mostrarMensajes)
        {
            var detExistente = detalleVenta.FirstOrDefault(d => d.ProductoId == producto.Id);

            if (producto.Id <= 0)
            {
                _snackbarQueue.Enqueue("Producto sin Id válido (no agregado).");
                return;
            }

            var nuevaCantidad = detExistente != null ? detExistente.Cantidad + 1 : 1;

            if (nuevaCantidad > producto.Stock)
            {
                var msg = $"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}";
                if (mostrarMensajes)
                    MessageBox.Show(msg, "Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    _snackbarQueue.Enqueue(msg);
                return;
            }

            if (detExistente != null)
            {
                detExistente.Cantidad = nuevaCantidad;
                detExistente.Subtotal = detExistente.Cantidad * detExistente.PrecioUnitario;
            }
            else
            {
                detalleVenta.Add(new DetalleVenta
                {
                    ProductoId = producto.Id,
                    ProductoNombre = producto.Nombre,
                    Cantidad = 1,
                    PrecioUnitario = producto.PrecioVenta,
                    Subtotal = producto.PrecioVenta
                });
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(producto.CodigoBarras))
                    _ultimoCodigoEscaneado = NormalizarCodigoParaBusqueda(producto.CodigoBarras);
                _ultimoCodigoEscaneadoTime = DateTime.UtcNow;
            }
            catch { }

            dgDetalleVenta.Items.Refresh();
            ActualizarTotales();
        }

        // Refresca un producto desde la base de datos considerando variantes de código
        private Producto? RefrescarProductoDesdeBD(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;

            string original = codigo.Trim();
            string sinCeros = original.TrimStart('0');
            string padded13 = (sinCeros.All(char.IsDigit) && sinCeros.Length > 0 && sinCeros.Length < 13)
                ? sinCeros.PadLeft(13, '0')
                : sinCeros;

            Producto? p =
                productoService.ObtenerPorCodigoBarras(original) ??
                (sinCeros != original ? productoService.ObtenerPorCodigoBarras(sinCeros) : null) ??
                (padded13 != original && padded13 != sinCeros ? productoService.ObtenerPorCodigoBarras(padded13) : null);

            if (p != null && _cacheListaLista && !string.IsNullOrWhiteSpace(p.CodigoBarras))
            {
                var key = p.CodigoBarras.Trim();
                _cacheProductosPorCodigo[key] = p;
            }
            return p;
        }
    }
}