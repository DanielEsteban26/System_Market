using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf; // <- agregado
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    /// <summary>
    /// Lógica de interacción para VentaWindow.xaml
    /// </summary>
    public partial class VentaWindow : Window
    {
        private readonly ProductoService productoService;
        private readonly VentaService ventaService;
        private ObservableCollection<DetalleVenta> detalleVenta;

        private int usuarioId = 1; // TODO: Cambiar por el usuario logueado
        private bool _codigoBloqueado; // única declaración

        // Longitud mínima para aceptar una entrada manual (evita residuos como "7")
        private const int MinManualCodeLength = 3;

        // Cola para Snackbar (2s)
        private readonly SnackbarMessageQueue _snackbarQueue = new(TimeSpan.FromSeconds(2));

        public VentaWindow()
        {
            InitializeComponent();
            productoService = new ProductoService(DatabaseInitializer.GetConnectionString());
            ventaService = new VentaService(DatabaseInitializer.GetConnectionString());
            detalleVenta = new ObservableCollection<DetalleVenta>();
            dgDetalleVenta.ItemsSource = detalleVenta;
            ActualizarTotales();

            // Enlaza la cola al Snackbar del XAML
            snackbar.MessageQueue = _snackbarQueue;

            // Por defecto: bloquear edición manual. El lector funcionará igual.
            BloquearEdicionCodigo();
            txtCodigoBarra.Clear();
        }

        // Constructor con código (bloquea caja y agrega automáticamente)
        public VentaWindow(string codigoInicial, bool bloquearCodigo = true) : this()
        {
            if (!string.IsNullOrWhiteSpace(codigoInicial))
            {
                _codigoBloqueado = bloquearCodigo;
                if (_codigoBloqueado)
                    BloquearEdicionCodigo();

                // Agregar el primer ítem (sin mensajes)
                AgregarProductoDesdeCodigo(codigoInicial.Trim(), mostrarMensajes: false);
                // Evitar residuo visual
                txtCodigoBarra.Clear();
            }
        }

        // Invocado por el lector cuando esta ventana está activa
        public void HandleScannedCode(string codigo)
        {
            // Muestra el código leído (solo esta lectura) y no dejes residuo
            txtCodigoBarra.Text = codigo;
            txtCodigoBarra.CaretIndex = codigo.Length;

            AgregarProductoDesdeCodigo(codigo.Trim(), mostrarMensajes: false);

            // Limpia para evitar acumulación visual
            txtCodigoBarra.Clear();
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

        // Bloquea entrada de texto cuando está en modo lector (evita el “7”)
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

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            var code = (txtCodigoBarra.Text ?? string.Empty).Trim();
            if (code.Length < MinManualCodeLength)
            {
                txtCodigoBarra.Focus();
                txtCodigoBarra.SelectAll();
                return;
            }
            AgregarProductoDesdeCodigo(code, mostrarMensajes: true);
        }

        private void AgregarProductoDesdeCodigo(string codigo, bool mostrarMensajes)
        {
            var producto = productoService.ObtenerPorCodigoBarras(codigo);
            if (producto == null)
            {
                if (mostrarMensajes)
                {
                    MessageBox.Show("Producto no encontrado.", "Información",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _snackbarQueue.Enqueue("Producto no encontrado.");
                }
                return;
            }

            var existente = detalleVenta.FirstOrDefault(d => d.ProductoId == producto.Id);
            int cantidadSolicitada = existente != null ? existente.Cantidad + 1 : 1;

            if (cantidadSolicitada > producto.Stock)
            {
                var msg = $"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}";
                if (mostrarMensajes)
                {
                    MessageBox.Show(msg, "Stock insuficiente",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    _snackbarQueue.Enqueue(msg);
                }
                return;
            }

            if (existente != null)
            {
                existente.Cantidad++;
                existente.Subtotal = existente.Cantidad * existente.PrecioUnitario;
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

            dgDetalleVenta.Items.Refresh();
            ActualizarTotales();

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
                    _snackbarQueue.Enqueue($"Producto no encontrado (ID: {det.ProductoId}).");
                    return;
                }

                int nuevaCantidad = det.Cantidad + 1;
                if (nuevaCantidad > producto.Stock)
                {
                    _snackbarQueue.Enqueue($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}");
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
                    det.Cantidad -= 1;
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
                    MessageBox.Show($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}, solicitado: {det.Cantidad}",
                        "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                var venta = new Venta
                {
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Estado = "Activa"
                };

                int ventaId = ventaService.AgregarVentaConDetalles(venta, detalleVenta.ToList());

                MessageBox.Show($"Venta registrada correctamente (ID: {ventaId}).");
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
                MessageBox.Show("Error al registrar la venta: " + ex.Message);
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

        // Botón/tecla para permitir escribir un código manual (SKU interno)
        private void BtnDesbloquearCodigo_Click(object sender, RoutedEventArgs e) => DesbloquearEdicionCodigo();

        private void BloquearEdicionCodigo()
        {
            _codigoBloqueado = true;
            txtCodigoBarra.IsReadOnly = true;
            txtCodigoBarra.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            txtCodigoBarra.Cursor = Cursors.Arrow;
            txtCodigoBarra.ToolTip = "F2 o 'Editar' para escribir un código manual.";
        }

        private void DesbloquearEdicionCodigo()
        {
            _codigoBloqueado = false;
            txtCodigoBarra.IsReadOnly = false;
            txtCodigoBarra.ClearValue(TextBox.BackgroundProperty);
            txtCodigoBarra.Cursor = Cursors.IBeam;
            txtCodigoBarra.ToolTip = null;
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
    }
}