using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class ProductoEdicionWindow : Window
    {
        public Producto Producto { get; private set; }
        private readonly CategoriaService _categoriaService;
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _productoService;
        private readonly bool _codigoBloqueado;
        private bool _esEdicion;

        private Brush _originalBorderBrush;

        private string? _ultimoCodigoVerificado;
        private bool _mostroDuplicado;

        private Dictionary<string, Producto> _indexProductos = new(StringComparer.OrdinalIgnoreCase);

        private const bool MostrarDebugNoEncontrado = false; // desactivar por defecto

        // Estado de unicidad para evitar mensajes intrusivos
        private Producto? _productoConEseCodigo;
        private bool _codigoExiste;

        // Permitir solo dígitos y separador decimal coma para precios (no puntos)
        private static readonly Regex RegexPrecioInput = new(@"^[0-9,]+$", RegexOptions.Compiled);

        // Permitir solo dígitos enteros para stock
        private static readonly Regex RegexEntero = new(@"^[0-9]+$", RegexOptions.Compiled);

        // Protección: descartar pulsaciones muy rápidas (scanner) en los TextBox con foco
        private DateTime _lastTextInputTime = DateTime.MinValue;
        private const double FastInputThresholdMs = 30.0; // ajusta si es necesario

        public ProductoEdicionWindow(string connectionString, Producto producto = null, string codigoPrefill = null, bool bloquearCodigo = false)
        {
            InitializeComponent();
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString);
            _productoService = new ProductoService(connectionString);
            _codigoBloqueado = bloquearCodigo;

            cbCategoria.ItemsSource = _categoriaService.ObtenerTodas();
            cbProveedor.ItemsSource = _proveedorService.ObtenerTodos();

            _originalBorderBrush = txtCodigoBarras.BorderBrush;

            ConstruirIndiceProductos();

            txtCodigoBarras.TextChanged += TxtCodigoBarras_TextChanged;
            txtCodigoBarras.PreviewKeyDown += TxtCodigoBarras_PreviewKeyDown;

            // Registramos handlers para los campos de precio (solo coma; NO formateo automático)
            txtPrecioCompra.PreviewTextInput += Precio_PreviewTextInput;
            txtPrecioVenta.PreviewTextInput += Precio_PreviewTextInput;
            DataObject.AddPastingHandler(txtPrecioCompra, Precio_PasteHandler);
            DataObject.AddPastingHandler(txtPrecioVenta, Precio_PasteHandler);

            // Registramos handlers para el campo de stock (solo enteros)
            txtStock.PreviewTextInput += Stock_PreviewTextInput;
            txtStock.PreviewKeyDown += Stock_PreviewKeyDown;
            DataObject.AddPastingHandler(txtStock, Stock_PasteHandler);

            // --- Protección contra "residuos" del escáner: registrar para los TextBox principales ---
            // Nota: NO registramos PreviewKeyDown aquí para evitar conflicto entre KeyDown y TextInput.
            var protectedTbs = new[] { txtCodigoBarras, txtNombre, txtPrecioCompra, txtPrecioVenta, txtStock };
            foreach (var tb in protectedTbs)
            {
                if (tb == null) continue;
                tb.PreviewTextInput += FastInput_PreviewTextInput;
                DataObject.AddPastingHandler(tb, ProtectedTextBox_PasteHandler);
            }

            if (producto != null)
            {
                _esEdicion = true;
                Producto = producto;
                txtCodigoBarras.Text = producto.CodigoBarras;
                txtNombre.Text = producto.Nombre;
                cbCategoria.SelectedValue = producto.CategoriaId;
                cbProveedor.SelectedValue = producto.ProveedorId;
                // Mostrar valores numéricos SIN prefijo "S/"
                txtPrecioCompra.Text = producto.PrecioCompra.ToString("F2", CultureInfo.CurrentCulture);
                txtPrecioVenta.Text = producto.PrecioVenta.ToString("F2", CultureInfo.CurrentCulture);
                txtStock.Text = producto.Stock.ToString(CultureInfo.CurrentCulture);
                Title = "Editar Producto";
            }
            else
            {
                Title = "Agregar Producto";
                if (!string.IsNullOrWhiteSpace(codigoPrefill))
                {
                    txtCodigoBarras.Text = NormalizarCodigo(codigoPrefill);
                    if (_codigoBloqueado) BloquearCodigo();
                    VerificarCodigoExistente(txtCodigoBarras.Text);
                }

                // No forzar formato; dejar vacío para que el usuario escriba
                txtPrecioCompra.Text = string.Empty;
                txtPrecioVenta.Text = string.Empty;
                txtStock.Text = string.Empty;
            }

            // Estado inicial del status textblock si existe
            if (txtEstadoCodigo != null && string.IsNullOrWhiteSpace(txtEstadoCodigo.Text))
                txtEstadoCodigo.Text = "Ingrese / escanee un código";
        }

        // --- Nuevo: handler de protección contra teclado rápido/escáner (solo TextInput) ---
        private void FastInput_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            var now = DateTime.UtcNow;
            // Si la última entrada fue muy reciente, descartamos (probable escaneo)
            if ((now - _lastTextInputTime).TotalMilliseconds < FastInputThresholdMs)
            {
                e.Handled = true;
                return;
            }

            // Aceptamos esta entrada y actualizamos tiempo
            _lastTextInputTime = now;
        }

        private void ProtectedTextBox_PasteHandler(object sender, DataObjectPastingEventArgs e)
        {
            // Evitar pegado inmediato si ha habido entrada muy rápida (probable escaner)
            var now = DateTime.UtcNow;
            if ((now - _lastTextInputTime).TotalMilliseconds < 200)
            {
                e.CancelCommand();
                return;
            }
            // Dejar comportamiento normal en otros casos
        }

        // Handler: sólo permitir dígitos y COMA. Si el usuario escribe punto, lo convertimos a coma.
        private void Precio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                e.Handled = true;
                return;
            }

            var input = e.Text;

            // Si escriben punto, lo convertimos a coma (si es válido)
            if (input == "." )
            {
                // Si ya hay una coma y no hay selección que la reemplace, rechazamos
                var proposed = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength).Insert(tb.SelectionStart, ",");
                if (proposed.Count(c => c == ',') > 1)
                {
                    e.Handled = true;
                    return;
                }

                // Insertar la coma manualmente
                tb.SelectedText = ",";
                // colocar caret después del carácter insertado
                tb.CaretIndex = tb.SelectionStart + 1;
                e.Handled = true;
                return;
            }

            // Validar que sólo se escriban dígitos o coma
            if (!RegexPrecioInput.IsMatch(input))
            {
                e.Handled = true;
                return;
            }

            // Si se intenta insertar coma y ya existe otra (fuera de selección), rechazar
            if (input == ",")
            {
                var proposed2 = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength).Insert(tb.SelectionStart, ",");
                if (proposed2.Count(c => c == ',') > 1)
                {
                    e.Handled = true;
                    return;
                }
            }

            // permitido
            e.Handled = false;
        }

        // Maneja pegado en los campos de precio: normaliza puntos->comas y valida formato (sólo dígitos y una coma)
        private void Precio_PasteHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }

            var raw = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!;
            if (string.IsNullOrWhiteSpace(raw))
            {
                e.CancelCommand();
                return;
            }

            // Normalizar: reemplazar puntos por comas y limpiar espacios
            var normalized = raw.Replace('.', ',').Trim();

            // Validación: sólo dígitos y una coma opcional, hasta 2 decimales
            if (!Regex.IsMatch(normalized, @"^\d+(,\d{0,2})?$"))
            {
                // si no cumple, cancelar pegado
                e.CancelCommand();
                return;
            }

            // Insertar el texto normalizado en el TextBox (cancelamos el pegado predeterminado)
            e.CancelCommand();
            if (sender is TextBox tb)
            {
                tb.SelectedText = normalized;
                tb.CaretIndex = tb.SelectionStart + normalized.Length;
            }
        }

        // --- resto del código existente (sin cambios en validaciones / lógica) ---
        private void ConstruirIndiceProductos()
        {
            try
            {
                var lista = _producto_service_obtener_todos_safe();
                var dict = new Dictionary<string, Producto>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in lista.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarras)))
                {
                    var raw = p.CodigoBarras!;
                    var n1 = NormalizarCodigo(raw);
                    var n2 = RemoverCerosIzquierda(n1);
                    var n3 = PadEAN13(n2);

                    void TryAdd(string k)
                    {
                        if (!string.IsNullOrEmpty(k) && !dict.ContainsKey(k))
                            dict[k] = p;
                    }

                    TryAdd(n1);
                    TryAdd(n2);
                    TryAdd(n3);
                }

                _indexProductos = dict;

#if DEBUG
                Debug.WriteLine($"[ProductoEdicion] Índice construido. Claves={_indexProductos.Count}");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ProductoEdicion] Error índice: " + ex.Message);
            }
        }

        private void TxtCodigoBarras_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
                e.Handled = true; // evita disparo accidental de Guardar
        }

        private static string NormalizarCodigo(string? codigo) =>
            new string((codigo ?? string.Empty).Trim().Where(c => !char.IsControl(c)).ToArray());

        private static string RemoverCerosIzquierda(string c) =>
            string.IsNullOrEmpty(c) ? c : c.TrimStart('0');

        private static string PadEAN13(string c) =>
            (c.All(char.IsDigit) && c.Length > 0 && c.Length < 13) ? c.PadLeft(13, '0') : c;

        // Método invocado por el BarcodeScannerService
        public void HandleScannedCode(string codigo)
        {
            codigo = NormalizarCodigo(codigo);
            if (string.IsNullOrEmpty(codigo)) return;

            // Rellena SOLO txtCodigoBarras
            txtCodigoBarras.Text = codigo;
            txtCodigoBarras.CaretIndex = codigo.Length;
            // Poner foco en el control de código para que quede claro al usuario:
            txtCodigoBarras.Focus();
        }

        private void TxtCodigoBarras_TextChanged(object sender, TextChangedEventArgs e)
        {
            VerificarCodigoExistente(txtCodigoBarras.Text);
        }

        // Actualiza un TextBlock de estado (txtEstadoCodigo) si exista
        private void ActualizarEstadoCodigo(string texto, Brush? color = null)
        {
            if (txtEstadoCodigo == null) return;
            txtEstadoCodigo.Text = texto;
            if (color != null) txtEstadoCodigo.Foreground = color;
        }

        private void VerificarCodigoExistente(string codigoEntrada)
        {
            var codigo = NormalizarCodigo(codigoEntrada);

            if (string.IsNullOrEmpty(codigo))
            {
                LimpiarResaltadoCodigo();
                _ultimoCodigoVerificado = null;
                _mostroDuplicado = false;
                _productoConEseCodigo = null;
                _codigoExiste = false;
                ActualizarEstadoCodigo("Ingrese / escanee un código");
                return;
            }

            Producto? existente = null;

            var variantes = new List<string> { codigo };
            var sinCeros = RemoverCerosIzquierda(codigo);
            if (!string.IsNullOrEmpty(sinCeros) && !variantes.Contains(sinCeros)) variantes.Add(sinCeros);
            var pad13 = PadEAN13(sinCeros);
            if (!string.IsNullOrEmpty(pad13) && !variantes.Contains(pad13)) variantes.Add(pad13);

            foreach (var v in variantes)
            {
                if (_indexProductos.TryGetValue(v, out existente))
                    break;
            }

            if (existente == null)
            {
                foreach (var v in variantes)
                {
                    var directo = _productoService.ObtenerPorCodigoBarras(v);
                    if (directo != null)
                    {
                        existente = directo;
                        try { _indexProductos[v] = directo; } catch { }
                        break;
                    }
                }
            }

            if (existente == null)
            {
                try
                {
                    var lista = _producto_service_obtener_todos_safe();
                    existente = lista.FirstOrDefault(p =>
                        !string.IsNullOrWhiteSpace(p.CodigoBarras) &&
                        NormalizarCodigo(p.CodigoBarras) == codigo);
                    if (existente != null)
                    {
                        try { _indexProductos[codigo] = existente; } catch { }
                    }
                }
                catch
                {
                }
            }

            if (existente != null && (!_esEdicion || existente.Id != (Producto?.Id ?? 0)))
            {
                txtCodigoBarras.BorderBrush = Brushes.OrangeRed;
                txtCodigoBarras.ToolTip = $"Código ya usado por: {existente.Nombre} (ID {existente.Id})";
                ActualizarEstadoCodigo($"Código USADO por: {existente.Nombre} (ID {existente.Id})", Brushes.OrangeRed);

                _productoConEseCodigo = existente;
                _codigoExiste = true;
                _mostroDuplicado = true;
            }
            else
            {
                LimpiarResaltadoCodigo();
                _mostroDuplicado = false;
                _productoConEseCodigo = null;
                _codigoExiste = false;
                ActualizarEstadoCodigo("Código libre", Brushes.LightGreen);
            }

            _ultimoCodigoVerificado = codigo;
        }

        private void LimpiarResaltadoCodigo()
        {
            txtCodigoBarras.BorderBrush = _originalBorderBrush;
            txtCodigoBarras.ToolTip = null;
        }

        private void BloquearCodigo()
        {
            txtCodigoBarras.IsReadOnly = true;
            txtCodigoBarras.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            txtCodigoBarras.Cursor = Cursors.Arrow;
            txtCodigoBarras.ToolTip = "Código fijado por lectura (no editable).";
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var codigo = NormalizarCodigo(txtCodigoBarras.Text);
            if (string.IsNullOrEmpty(codigo))
            {
                ActualizarEstadoCodigo("Código de barras inválido.", Brushes.OrangeRed);
                txtCodigoBarras.Focus();
                txtCodigoBarras.SelectAll();
                return;
            }

            if (_codigoExiste && (_esEdicion == false || (_productoConEseCodigo?.Id ?? 0) != (Producto?.Id ?? 0)))
            {
                MessageBox.Show($"No se puede guardar. El código ya pertenece a '{_productoConEseCodigo?.Nombre}' (ID {_productoConEseCodigo?.Id}).",
                    "Código duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                txtCodigoBarras.Focus();
                txtCodigoBarras.SelectAll();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                ActualizarEstadoCodigo("Complete el nombre del producto.", Brushes.OrangeRed);
                txtNombre.Focus();
                return;
            }

            if (cbCategoria.SelectedValue == null)
            {
                ActualizarEstadoCodigo("Seleccione una categoría.", Brushes.OrangeRed);
                cbCategoria.Focus();
                return;
            }

            if (cbProveedor.SelectedValue == null)
            {
                ActualizarEstadoCodigo("Seleccione un proveedor.", Brushes.OrangeRed);
                cbProveedor.Focus();
                return;
            }

            if (!CurrencyService.TryParseSoles(txtPrecioCompra.Text, out decimal precioCompra) || precioCompra < 0)
            {
                ActualizarEstadoCodigo("Ingrese un precio de compra válido.", Brushes.OrangeRed);
                txtPrecioCompra.Focus();
                txtPrecioCompra.SelectAll();
                return;
            }

            if (!CurrencyService.TryParseSoles(txtPrecioVenta.Text, out decimal precioVenta) || precioVenta < 0)
            {
                ActualizarEstadoCodigo("Ingrese un precio de venta válido.", Brushes.OrangeRed);
                txtPrecioVenta.Focus();
                txtPrecioVenta.SelectAll();
                return;
            }

            if (!int.TryParse(txtStock.Text, out int stock) || stock < 0)
            {
                ActualizarEstadoCodigo("Ingrese una cantidad de stock válida.", Brushes.OrangeRed);
                txtStock.Focus();
                txtStock.SelectAll();
                return;
            }

            if (_esEdicion && Producto != null)
            {
                var cambios = "";
                if (Producto.CodigoBarras != codigo)
                    cambios += $"- Código de barras: '{Producto.CodigoBarras}' → '{codigo}'\n";
                if (Producto.Nombre != txtNombre.Text.Trim())
                    cambios += $"- Nombre: '{Producto.Nombre}' → '{txtNombre.Text.Trim()}'\n";
                if (Producto.CategoriaId != (int)cbCategoria.SelectedValue)
                    {
                    var catAnt = (cbCategoria.ItemsSource as IEnumerable<Categoria>)?.FirstOrDefault(c => c.Id == Producto.CategoriaId)?.Nombre ?? "";
                    var catNue = (cbCategoria.SelectedItem as Categoria)?.Nombre ?? "";
                    cambios += $"- Categoría: '{catAnt}' → '{catNue}'\n";
                    }
                if (Producto.ProveedorId != (int)cbProveedor.SelectedValue)
                {
                    var provAnt = (cbProveedor.ItemsSource as IEnumerable<Proveedor>)?.FirstOrDefault(p => p.Id == Producto.ProveedorId)?.Nombre ?? "";
                    var provNue = (cbProveedor.SelectedItem as Proveedor)?.Nombre ?? "";
                    cambios += $"- Proveedor: '{provAnt}' → '{provNue}'\n";
                }
                if (Producto.PrecioCompra != precioCompra)
                    cambios += $"- Precio de compra: {CurrencyService.FormatNumber(Producto.PrecioCompra, "N2")} → {CurrencyService.FormatNumber(precioCompra, "N2")}\n";
                if (Producto.PrecioVenta != precioVenta)
                    cambios += $"- Precio de venta: {CurrencyService.FormatNumber(Producto.PrecioVenta, "N2")} → {CurrencyService.FormatNumber(precioVenta, "N2")}\n";
                if (Producto.Stock != stock)
                    cambios += $"- Stock: {Producto.Stock} → {stock}\n";

                if (!string.IsNullOrWhiteSpace(cambios))
                {
                    var msg = "¿Confirma los siguientes cambios?\n\n" + cambios;
                    var result = MessageBox.Show(msg, "Confirmar actualización", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                        return;
                }
            }
            else
            {
                // Confirmación al agregar: mostrar resumen del nuevo producto
                var catNombre = (cbCategoria.SelectedItem as Categoria)?.Nombre ?? "";
                var provNombre = (cbProveedor.SelectedItem as Proveedor)?.Nombre ?? "";
                var resumen = $"Se agregará el siguiente producto:\n\n" +
                              $"- Código: {codigo}\n" +
                              $"- Nombre: {txtNombre.Text.Trim()}\n" +
                              $"- Categoría: {catNombre}\n" +
                              $"- Proveedor: {provNombre}\n" +
                              $"- Precio compra: {CurrencyService.FormatNumber(precioCompra, "N2")}\n" +
                              $"- Precio venta: {CurrencyService.FormatNumber(precioVenta, "N2")}\n" +
                              $"- Stock: {stock}\n\n¿Desea continuar?";
                var confirmAdd = MessageBox.Show(resumen, "Confirmar agregado", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmAdd != MessageBoxResult.Yes)
                    return;
            }

            ActualizarEstadoCodigo("Listo", Brushes.LightGreen);

            Producto ??= new Producto();
            Producto.CodigoBarras = codigo;
            Producto.Nombre = txtNombre.Text.Trim();
            Producto.CategoriaId = (int)cbCategoria.SelectedValue;
            Producto.ProveedorId = (int)cbProveedor.SelectedValue;
            Producto.PrecioCompra = precioCompra;
            Producto.PrecioVenta = precioVenta;
            Producto.Stock = stock;

            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        public void CargarDatosDesdeCodigoBarras(string codigo)
        {
            txtCodigoBarras.Text = codigo;
        }

        private void Stock_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !RegexEntero.IsMatch(e.Text);
        }

        private void Stock_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }
        }

        private void Stock_PasteHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }
            var txt = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!;
            if (!RegexEntero.IsMatch(txt))
                e.CancelCommand();
        }

        private List<Producto> _producto_service_obtener_todos_safe()
        {
            try { return _productoService.ObtenerTodos(); }
            catch { return new List<Producto>(); }
        }
    }
}