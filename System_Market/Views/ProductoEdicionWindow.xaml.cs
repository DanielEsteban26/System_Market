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

        public ProductoEdicionWindow(string connectionString,
                                     Producto producto = null,
                                     string codigoPrefill = null,
                                     bool bloquearCodigo = false)
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

            // 1) Buscar en índice
            _indexProductos.TryGetValue(codigo, out var existente);

            // 2) Si no, intento directo BD (por si se creó recientemente en otra ventana)
            if (existente == null)
            {
                var directo = _productoService.ObtenerPorCodigoBarras(codigo);
                if (directo != null)
                {
                    existente = directo;
                    // Actualizar índice
                    _indexProductos[codigo] = directo;
#if DEBUG
                    Debug.WriteLine($"[ProductoEdicion] Encontrado por consulta directa: {codigo} -> {directo.Nombre}");
#endif
                }
            }

            // 3) Fallback final: enumerar (muy raro que llegue aquí si índice está bien)
            if (existente == null)
            {
                var lista = _productoService.ObtenerTodos();
                existente = lista.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.CodigoBarras) &&
                    NormalizarCodigo(p.CodigoBarras) == codigo);
                if (existente != null)
                {
                    _indexProductos[codigo] = existente;
#if DEBUG
                    Debug.WriteLine($"[ProductoEdicion] Encontrado en fallback enumerado: {codigo} -> {existente.Nombre}");
#endif
                }
            }

            if (existente != null && (!_esEdicion || existente.Id != (Producto?.Id ?? 0)))
            {
                // No mostrar MessageBox aquí: solo resaltar y actualizar estado
                txtCodigoBarras.BorderBrush = Brushes.OrangeRed;
                txtCodigoBarras.ToolTip = $"Código ya usado por: {existente.Nombre} (ID {existente.Id})";
                ActualizarEstadoCodigo($"Código USADO por: {existente.Nombre} (ID {existente.Id})", Brushes.OrangeRed);

                _productoConEseCodigo = existente;
                _codigoExiste = true;
                _mostroDuplicado = true; // marca para saber que hay duplicado visualmente
            }
            else
            {
                LimpiarResaltadoCodigo();
                _mostroDuplicado = false;
                _productoConEseCodigo = null;
                _codigoExiste = false;
                ActualizarEstadoCodigo("Código libre", Brushes.LightGreen);
#if DEBUG
                Debug.WriteLine($"[ProductoEdicion] Código libre: {codigo}");
#endif
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
                MessageBox.Show("Código de barras inválido.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validación básica de otros campos
            if (string.IsNullOrWhiteSpace(txtNombre.Text) ||
                cbCategoria.SelectedValue == null ||
                cbProveedor.SelectedValue == null ||
                !decimal.TryParse(txtPrecioCompra.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precioCompra) ||
                !decimal.TryParse(txtPrecioVenta.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precioVenta) ||
                !int.TryParse(txtStock.Text, out int stock))
            {
                MessageBox.Show("Complete todos los campos correctamente.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Unicidad: si detectamos un producto distinto con ese código, impedir guardar
            if (_codigoExiste && (_esEdicion == false || (_productoConEseCodigo?.Id ?? 0) != (Producto?.Id ?? 0)))
            {
                MessageBox.Show($"No se puede guardar. El código ya pertenece a '{_productoConEseCodigo?.Nombre}' (ID {_productoConEseCodigo?.Id}).",
                    "Código duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                txtCodigoBarras.Focus();
                txtCodigoBarras.SelectAll();
                return;
            }

            Producto ??= new Producto();
            Producto.CodigoBarras = codigo;
            Producto.Nombre = txtNombre.Text.Trim();
            Producto.CategoriaId = (int)cbCategoria.SelectedValue;
            Producto.ProveedorId = (int)cbProveedor.SelectedValue;
            Producto.PrecioCompra = precioCompra;
            Producto.PrecioVenta = precioVenta;
            Producto.Stock = stock;

            if (Producto.Stock < 0)
            {
                MessageBox.Show("El stock no puede ser negativo.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}