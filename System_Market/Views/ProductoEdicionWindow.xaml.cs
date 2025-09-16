using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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

        public ProductoEdicionWindow(string connectionString, Producto producto = null, string codigoPrefill = null, bool bloquearCodigo = false)
        {
            InitializeComponent();
            _categoriaService = new CategoriaService(connectionString);
            _proveedorService = new ProveedorService(connectionString);
            _productoService  = new ProductoService(connectionString);
            _codigoBloqueado  = bloquearCodigo;

            cbCategoria.ItemsSource = _categoriaService.ObtenerTodas();
            cbProveedor.ItemsSource = _proveedorService.ObtenerTodos();

            _originalBorderBrush = txtCodigoBarras.BorderBrush;

            ConstruirIndiceProductos();

            txtCodigoBarras.TextChanged += TxtCodigoBarras_TextChanged;
            txtCodigoBarras.PreviewKeyDown += TxtCodigoBarras_PreviewKeyDown;

            if (producto != null)
            {
                _esEdicion = true;
                Producto = producto;
                txtCodigoBarras.Text = producto.CodigoBarras;
                txtNombre.Text = producto.Nombre;
                cbCategoria.SelectedValue = producto.CategoriaId;
                cbProveedor.SelectedValue = producto.ProveedorId;
                txtPrecioCompra.Text = producto.PrecioCompra.ToString(CultureInfo.InvariantCulture);
                txtPrecioVenta.Text = producto.PrecioVenta.ToString(CultureInfo.InvariantCulture);
                txtStock.Text = producto.Stock.ToString();
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
            }

            // Estado inicial del status textblock si existe
            if (txtEstadoCodigo != null && string.IsNullOrWhiteSpace(txtEstadoCodigo.Text))
                txtEstadoCodigo.Text = "Ingrese / escanee un código";
        }

        private void ConstruirIndiceProductos()
        {
            try
            {
                var lista = _productoService.ObtenerTodos();
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

            // Solo rellenar el textbox y validar visualmente, sin mostrar MessageBox intrusivo
            txtCodigoBarras.Text = codigo; // Dispara TextChanged -> VerificarCodigoExistente
            txtCodigoBarras.CaretIndex = codigo.Length;
        }

        private void TxtCodigoBarras_TextChanged(object sender, TextChangedEventArgs e)
        {
            VerificarCodigoExistente(txtCodigoBarras.Text);
        }

        // Actualiza un TextBlock de estado (txtEstadoCodigo) si existe
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

            // Construir variantes de búsqueda (normal, sin ceros y padded EAN13)
            var variantes = new List<string> { codigo };
            var sinCeros = RemoverCerosIzquierda(codigo);
            if (!string.IsNullOrEmpty(sinCeros) && !variantes.Contains(sinCeros)) variantes.Add(sinCeros);
            var pad13 = PadEAN13(sinCeros);
            if (!string.IsNullOrEmpty(pad13) && !variantes.Contains(pad13)) variantes.Add(pad13);

            // 1) Buscar en índice por cualquiera de las variantes
            foreach (var v in variantes)
            {
                if (_indexProductos.TryGetValue(v, out existente))
                    break;
            }

            // 2) Si no encontrado, intento directo en BD para cada variante
            if (existente == null)
            {
                foreach (var v in variantes)
                {
                    var directo = _productoService.ObtenerPorCodigoBarras(v);
                    if (directo != null)
                    {
                        existente = directo;
                        // actualizar índice para futuras búsquedas
                        try { _indexProductos[v] = directo; } catch { }
                        break;
                    }
                }
            }

            // 3) Fallback: enumerar todos y comparar normalizado (por si hay otros formatos)
            if (existente == null)
            {
                try
                {
                    var lista = _productoService.ObtenerTodos();
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
                    // ignorar
                }
            }

            // Si existe y no es el mismo registro cuando estamos en edición -> marcado de duplicado
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

            // Unicidad: si detectamos un producto distinto con ese código, impedir guardar (modal)
            if (_codigoExiste && (_esEdicion == false || (_productoConEseCodigo?.Id ?? 0) != (Producto?.Id ?? 0)))
            {
                MessageBox.Show($"No se puede guardar. El código ya pertenece a '{_productoConEseCodigo?.Nombre}' (ID {_productoConEseCodigo?.Id}).",
                    "Código duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                txtCodigoBarras.Focus();
                txtCodigoBarras.SelectAll();
                return;
            }

            // Validación por campos: NOTIFICACIONES INLINE en lugar de modal
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

            if (!decimal.TryParse(txtPrecioCompra.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precioCompra) || precioCompra < 0)
            {
                ActualizarEstadoCodigo("Ingrese un precio de compra válido.", Brushes.OrangeRed);
                txtPrecioCompra.Focus();
                txtPrecioCompra.SelectAll();
                return;
            }

            if (!decimal.TryParse(txtPrecioVenta.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precioVenta) || precioVenta < 0)
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

            // --- CONFIRMACIÓN DE CAMBIOS EN EDICIÓN ---
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
                    cambios += $"- Precio de compra: {Producto.PrecioCompra} → {precioCompra}\n";
                if (Producto.PrecioVenta != precioVenta)
                    cambios += $"- Precio de venta: {Producto.PrecioVenta} → {precioVenta}\n";
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

            // Todo OK: limpiar estado y aceptar
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
            // Puedes agregar más lógica si lo necesitas
        }
    }
}