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
    public partial class VentaWindow : Window
    {
        private readonly ProductoService productoService;
        private readonly VentaService ventaService;
        private ObservableCollection<DetalleVenta> detalleVenta;

        // Ya no inicializamos a 1 por defecto; tomamos el Id del usuario en sesión en el constructor.
        private int usuarioId;
        private bool _codigoBloqueado;

        private const int MinManualCodeLength = 3;
        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(2));

        private string? _ultimoCodigoEscaneado;

        // --- NUEVO: timestamp del último código procesado para evitar duplicados rápidos ---
        private DateTime _ultimoCodigoEscaneadoTime = DateTime.MinValue;

        // Cache / control carga
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

            // Establecer usuario actual (si hay sesión)
            usuarioId = System_Market.Models.SesionActual.Usuario?.Id ?? 1;

            // Escaneos que ocurrieron SIN ventana de ventas abierta
            DrenarEscaneosPendientes();

            // NOTE: TryReplay moved to Loaded to avoid double-add when constructor overload provides an initial code.
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

                // Marcar que acabamos de procesar este código (evita replay desde TryReplayLastCodeFor/Loaded)
                try
                {
                    _ultimoCodigoEscaneado = NormalizarCodigoParaBusqueda(codigoInicial);
                    _ultimoCodigoEscaneadoTime = DateTime.UtcNow;
                }
                catch { /* no crítico */ }
            }
        }

        private void VentaWindow_Activated(object? sender, EventArgs e)
        {
            // Drenar nuevamente por si llegó algo entre Loaded y Activated
            DrenarEscaneosPendientes();
        }

        private async void DrenarEscaneosPendientes()
        {
            var list = BarcodeScannerService.DrainPendingVentaCodes();
            if (list.Count == 0) return;
            foreach (var c in list)
                HandleScannedCode(c);
        }

        private async void VentaWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await CargarCacheProductosAsync();

            // Ocultar el botón "Agregar producto" en la ventana de ventas (si existe)
            // Evita referencias directas a un control que puede haber sido eliminado del XAML.
            try
            {
                if (this.FindName("btnAgregarProducto") is Button btn)
                {
                    btn.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // No romper la carga si algo falla; FindName no debería lanzar, pero por seguridad dejamos el catch.
            }

            // Replay tras tener cache (solo aquí para evitar doble procesamiento al crear con código inicial)
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

        public void HandleScannedCode(string codigo)
        {
            var normalizado = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(normalizado)) return;

            // Evitar procesar repetidos muy cercanos en el tiempo (dobles/triples por reentradas)
            if (!string.IsNullOrEmpty(_ultimoCodigoEscaneado) &&
                string.Equals(_ultimoCodigoEscaneado, normalizado, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.UtcNow - _ultimoCodigoEscaneadoTime).TotalMilliseconds < 800)
            {
                // Ignorar duplicado rápido
                return;
            }

            // NOTA: no filtrar por letras aquí — aceptamos cualquier formato de código.
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
                // mostrarMensajes = false: para escaneos automáticos preferimos no mostrar MessageBox modal
                AgregarProductoDesdeCodigo(normalizado, mostrarMensajes: false);
            }

            // Limpiar el TextBox programáticamente (no afecta al flujo de escaneo)
            txtCodigoBarra.Clear();
        }

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

        private static string NormalizarCodigoParaBusqueda(string? codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
            // Trim + elimina caracteres de control (a veces los scanners incluyen \r o \n)
            var trimmed = codigo.Trim();
            return new string(trimmed.Where(ch => !char.IsControl(ch)).ToArray());
        }

        private async void AgregarProductoDesdeCodigo(string codigo, bool mostrarMensajes)
        {
            codigo = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(codigo)) return;

            // 1. Cache rápida
            Producto? producto = BuscarProductoEnCacheODB(codigo);

            // 2. Pequeño delay (por si DB se acaba de actualizar desde otra ventana)
            if (producto == null)
            {
                await Task.Delay(40);
                producto = BuscarProductoEnCacheODB(codigo);
            }

            // 3. Refresco amplio (variantes)
            if (producto == null)
                producto = RefrescarProductoDesdeBD(codigo);

            // --- NUEVO: fallback: buscar entre los productos ya añadidos en la venta ---
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

            // 4. Si sigue null -> ahora mostramos mensaje informativo y sugerimos ir a Compras/Productos
            if (producto == null)
            {
                var mensaje = $"Producto no encontrado para el código '{codigo}'.\n\n" +
                              "Sugerencia: vaya a 'Compras' o 'Productos' para crearlo o registrarlo antes de venderlo.";
                MessageBox.Show(mensaje, "Producto no encontrado", MessageBoxButton.OK, MessageBoxImage.Information);

                // También mostrar en snackbar una pista rápida
                _snackbarQueue.Enqueue("Producto no encontrado. Abrir Compras o Productos para crearlo/registrarlo.");

                return;
            }

            AgregarOIncrementarProducto(producto, mostrarMensajes);
        }

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

        // --- NUEVO: búsqueda robusta unificada (cache + variantes BD) ---
        private Producto? BuscarProductoRobusto(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;
            var norm = NormalizarCodigoParaBusqueda(codigo);
            if (string.IsNullOrEmpty(norm)) return null;

            // Cache / BD directa / variantes
            return BuscarProductoEnCacheODB(norm) ?? RefrescarProductoDesdeBD(norm);
        }

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_creacionProductoEnCurso) return;

            var codeTyped = (txtCodigoBarra.Text ?? string.Empty).Trim();

            // 1. Sin código: informamos que la creación desde ventas está deshabilitada
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

            // 2. Con código: buscar si ya existe (robusto)
            var existente = BuscarProductoRobusto(codeTyped);

            if (existente != null)
            {
                // Incrementar directamente (NO abrir creación)
                AgregarOIncrementarProducto(existente, mostrarMensajes: false);
                // Si quieres mensaje tipo snackbar:
                _snackbarQueue.Enqueue($"Cantidad de '{existente.Nombre}' actualizada.");

                if (!_codigoBloqueado)
                {
                    txtCodigoBarra.Clear();
                    txtCodigoBarra.Focus();
                }
                return;
            }

            // 3. No existe: mostrar mensaje informativo y sugerir ir a Compras/Productos
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

        private void dgDetalleVenta_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && dgDetalleVenta.SelectedItem is DetalleVenta seleccionado)
            {
                detalleVenta.Remove(seleccionado);
                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

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

            // Construir resumen para confirmación previa
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
                    // Aseguramos usar el usuario de la sesión al momento de guardar
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

        private void ActualizarTotales()
        {
            decimal total = detalleVenta.Sum(d => d.Subtotal);
            txtTotal.Text = $"Total: {total:C}";
        }

        private void BtnHistorial_Click(object sender, RoutedEventArgs e)
        {
            var historial = new HistorialVentasWindow();
            historial.ShowDialog();
        }

        private void BtnDesbloquearCodigo_Click(object sender, RoutedEventArgs e) => DesbloquearEdicionCodigo();

        private void BloquearEdicionCodigo()
        {
            _codigoBloqueado = true;

            // Evitar que el TextBox reciba foco o interacción del usuario
            txtCodigoBarra.IsReadOnly = true;
            txtCodigoBarra.IsHitTestVisible = false; // no recibirá clics ni permitirá selección
            txtCodigoBarra.Focusable = false;        // no podrá recibir foco por teclado
            txtCodigoBarra.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            txtCodigoBarra.Cursor = Cursors.Arrow;
            txtCodigoBarra.ToolTip = "F2 o 'Editar' para escribir un código manual.";
        }

        private void DesbloquearEdicionCodigo()
        {
            _codigoBloqueado = false;

            txtCodigoBarra.IsReadOnly = false;
            txtCodigoBarra.IsHitTestVisible = true;
            txtCodigoBarra.Focusable = true;
            txtCodigoBarra.ClearValue(TextBox.BackgroundProperty);
            txtCodigoBarra.Cursor = Cursors.IBeam;
            txtCodigoBarra.ToolTip = null;

            // Poner foco y seleccionar todo para facilitar la edición manual
            txtCodigoBarra.Focus();
            txtCodigoBarra.SelectAll();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                DesbloquearEdicionCodigo();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        // Reemplaza el método CrearProductoInteractivo por esta versión con control de reentrada:
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
                    // Guardar en BD
                    productoService.AgregarProducto(win.Producto);

                    // Re-consultar para asegurar Id real
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

        private void AgregarOIncrementarProducto(Producto producto, bool mostrarMensajes)
        {
            var detExistente = detalleVenta.FirstOrDefault(d => d.ProductoId == producto.Id);

            // Validar Id correcto
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

            // MARCAR código procesado (evita reentrada por replay/pendientes)
            try
            {
                if (!string.IsNullOrWhiteSpace(producto.CodigoBarras))
                    _ultimoCodigoEscaneado = NormalizarCodigoParaBusqueda(producto.CodigoBarras);
                _ultimoCodigoEscaneadoTime = DateTime.UtcNow;
            }
            catch { /* no crítico */ }

            dgDetalleVenta.Items.Refresh();
            ActualizarTotales();
        }

        private Producto? RefrescarProductoDesdeBD(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;

            // variantes
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