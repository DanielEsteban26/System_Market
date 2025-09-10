using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        public VentaWindow()
        {
            InitializeComponent();
            productoService = new ProductoService(DatabaseInitializer.GetConnectionString());
            ventaService = new VentaService(DatabaseInitializer.GetConnectionString());
            detalleVenta = new ObservableCollection<DetalleVenta>();
            dgDetalleVenta.ItemsSource = detalleVenta;
            ActualizarTotales();
        }

        private void txtCodigoBarra_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnAgregarProducto_Click(sender, e);
            }
        }

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            string codigo = txtCodigoBarra.Text.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                MessageBox.Show("Ingrese un código de barras válido.");
                return;
            }

            var producto = productoService.ObtenerPorCodigoBarras(codigo);
            if (producto == null)
            {
                MessageBox.Show("Producto no encontrado.");
                return;
            }

            var existente = detalleVenta.FirstOrDefault(d => d.ProductoId == producto.Id);
            int cantidadSolicitada = existente != null ? existente.Cantidad + 1 : 1;

            if (cantidadSolicitada > producto.Stock)
            {
                MessageBox.Show($"No hay suficiente stock para '{producto.Nombre}'. Stock disponible: {producto.Stock}, solicitado: {cantidadSolicitada}", "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            txtCodigoBarra.Clear();
            txtCodigoBarra.Focus();
        }

        // NUEVO: aumentar cantidad (+)
        private void btnAumentarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is DetalleVenta det)
            {
                // Validar stock actual antes de aumentar
                var producto = productoService.ObtenerTodos().FirstOrDefault(p => p.Id == det.ProductoId);
                if (producto == null)
                {
                    MessageBox.Show($"Producto no encontrado (ID: {det.ProductoId}).");
                    return;
                }

                int nuevaCantidad = det.Cantidad + 1;
                if (nuevaCantidad > producto.Stock)
                {
                    MessageBox.Show($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}", "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                det.Cantidad = nuevaCantidad;
                det.Subtotal = det.Cantidad * det.PrecioUnitario;

                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

        // NUEVO: disminuir cantidad (-). Si llega a 0, elimina el ítem
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
                    // Cantidad = 1 y se presiona disminuir: remover
                    detalleVenta.Remove(det);
                }

                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }

        // Eliminar con tecla Supr (opcional, se mantiene)
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

            // Validar stock de todos los productos antes de registrar la venta
            foreach (var detalle in detalleVenta)
            {
                var producto = productoService.ObtenerTodos().FirstOrDefault(p => p.Id == detalle.ProductoId);
                if (producto == null)
                {
                    MessageBox.Show($"Producto no encontrado (ID: {detalle.ProductoId}).");
                    return;
                }
                if (detalle.Cantidad > producto.Stock)
                {
                    MessageBox.Show($"No hay suficiente stock para '{producto.Nombre}'. Stock disponible: {producto.Stock}, solicitado: {detalle.Cantidad}", "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        // Obsoleto si ya no usas el botón Eliminar en la grilla, se deja por compatibilidad
        private void btnEliminarProducto_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            int productoId = int.Parse(btn!.Tag.ToString()!);
            var item = detalleVenta.FirstOrDefault(d => d.ProductoId == productoId);
            if (item != null)
            {
                detalleVenta.Remove(item);
                dgDetalleVenta.Items.Refresh();
                ActualizarTotales();
            }
        }
    }
}       