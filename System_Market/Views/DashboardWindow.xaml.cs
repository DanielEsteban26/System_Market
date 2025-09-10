using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly string _conn;
        private readonly ProductoService _productoService;
        private readonly VentaService _ventaService;
        private readonly CompraService _compraService;

        private readonly string _usuarioActualNombre; // NUEVO

        private int _umbralStock = 5;
        private decimal? _metaVentasMes;

        private readonly string _metaConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "System_Market", "meta_mes.json");

        private readonly string _layoutConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "System_Market", "dashboard_layout.json");

        private Point _dragStartPoint;
        private FrameworkElement? _dragSourceCard;
        private bool _layoutLocked;
        private bool _modoCompacto;
        private List<string>? _defaultOrder;

        private readonly ObservableCollection<QuickAction> _accionesRapidas = new();
        private readonly List<AccionDef> _catalogoAcciones = new()
        {
            new("NuevaVenta", "Nueva Venta", "\uE73E"),
            new("NuevaCompra", "Nueva Compra", "\uE73E"),
            new("AgregarProducto", "Agregar Producto", "\uE710"),
            new("NuevoProveedor", "Proveedor Nuevo", "\uE716"),
            new("Productos", "Listado Productos", "\uE14C"),
            new("Ventas", "Listado Ventas", "\uE7BF"),
            new("Compras", "Listado Compras", "\uE73E"),
            new("Proveedores", "Listado Proveedores", "\uE716"),
            new("Usuarios", "Gestión Usuarios", "\uE77B"),
            new("Categorias", "Gestión Categorías", "\uE1CB"),
            new("Dashboard", "Dashboard", "\uE9D5")
        };

        private string QuickActionsFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "System_Market", "quick_actions_dashboard.json");

        // Constructor principal con usuario
        public DashboardWindow(string usuarioActualNombre)
        {
            _usuarioActualNombre = string.IsNullOrWhiteSpace(usuarioActualNombre) ? "Usuario" : usuarioActualNombre;
            InitializeComponent();

            _conn = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(_conn);
            _ventaService = new VentaService(_conn);
            _compraService = new CompraService(_conn);

            MainSnackbar.MessageQueue ??= new SnackbarMessageQueue(TimeSpan.FromSeconds(4));
            CargarMetaDesdeArchivo();

            icQuickActions.ItemsSource = _accionesRapidas;
            cbAccion.ItemsSource = _catalogoAcciones;

            if (!CargarAccionesGuardadas())
            {
                AgregarAccionSiNoExiste("NuevaVenta");
                AgregarAccionSiNoExiste("NuevaCompra");
                AgregarAccionSiNoExiste("AgregarProducto");
                AgregarAccionSiNoExiste("NuevoProveedor");
                GuardarAcciones();
            }

            Closed += (_, __) => GuardarAcciones();
        }

        // Constructor legacy (por compatibilidad). Elimina este si ya actualizaste todos los llamados.
        public DashboardWindow() : this("Usuario Demo") { }

        #region Meta
        private void CargarMetaDesdeArchivo()
        {
            try
            {
                if (File.Exists(_metaConfigPath))
                {
                    var json = File.ReadAllText(_metaConfigPath);
                    if (decimal.TryParse(json, out var meta) && meta > 0)
                        _metaVentasMes = meta;
                }
            }
            catch { }
        }
        private void GuardarMetaEnArchivo()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_metaConfigPath)!);
                File.WriteAllText(_metaConfigPath, _metaVentasMes?.ToString() ?? "");
            }
            catch { }
        }
        #endregion

        #region Carga
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeScannerService.Start();

            txtUsuarioActual.Text = _usuarioActualNombre;
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;

            CapturarOrdenPorDefecto();
            ReordenarSegunLayout();
            await CargarTodoAsync();
        }

        private async Task CargarTodoAsync()
        {
            try
            {
                MostrarOverlay(true);

                var desde = (dpDesde.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

                if (!int.TryParse(txtUmbralStock.Text.Trim(), out _umbralStock) || _umbralStock < 0)
                    _umbralStock = 5;

                var productos = await Task.Run(_productoService.ObtenerTodos);
                txtTotalProductos.Text = productos.Count.ToString("N0");

                var stockBajo = productos.Where(p => p.Stock <= _umbralStock).ToList();
                txtStockBajo.Text = stockBajo.Count.ToString("N0");
                txtPorcStockBajo.Text = productos.Count == 0
                    ? "0%"
                    : (stockBajo.Count * 100m / productos.Count).ToString("N1") + "%";
                dgStockBajo.ItemsSource = stockBajo;

                var ventas = await Task.Run(_ventaService.ObtenerTodas);
                var ventasRango = ventas
                    .Where(v => v.Estado == "Activa" && v.Fecha >= desde && v.Fecha <= hasta)
                    .OrderBy(v => v.Fecha)
                    .ToList();

                var compras = await Task.Run(_compraService.ObtenerTodas);
                var comprasRango = compras
                    .Where(c => c.Estado == "Activa" && c.Fecha >= desde && c.Fecha <= hasta)
                    .ToList();

                txtVentasRango.Text = "S/ " + ventasRango.Sum(v => v.Total).ToString("N2");
                txtComprasRango.Text = "S/ " + comprasRango.Sum(c => c.Total).ToString("N2");
                txtCantidadVentas.Text = ventasRango.Count.ToString("N0");

                var top = await Task.Run(() => ObtenerTopVendidos(desde, hasta, 5));
                lvTopVendidos.ItemsSource = top;
                txtProductoMasVendido.Text = top.FirstOrDefault()?.NombreConCantidad ?? "Sin datos";

                if (stockBajo.Count > 0)
                    MainSnackbar.MessageQueue?.Enqueue($"{stockBajo.Count} producto(s) con stock bajo (≤ {_umbralStock}).");

                int unidadesRango = await Task.Run(() => ObtenerUnidadesVendidas(desde, hasta));
                txtUnidadesRango.Text = unidadesRango.ToString("N0");
                int unidadesHoy = await Task.Run(() =>
                    ObtenerUnidadesVendidas(DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1)));
                txtUnidadesHoy.Text = unidadesHoy.ToString("N0");

                if (unidadesRango > 0)
                {
                    decimal totalRango = ventasRango.Sum(v => v.Total);
                    txtVentasRango.ToolTip = $"Ticket promedio (rango): S/ {(totalRango / unidadesRango):N2}";
                }
                else
                {
                    txtVentasRango.ClearValue(ToolTipProperty);
                }

                CalcularIndicadoresDiaYMeta(ventas);
                AplicarColoresSemanticos();
                ActualizarSparklineUltimos7Dias(ventas);
                AnimarAparicionTarjetas();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar dashboard: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MostrarOverlay(false);
            }
        }
        #endregion

        #region Cálculos
        private List<TopVentaDTO> ObtenerTopVendidos(DateTime desde, DateTime hasta, int limite)
        {
            var lista = new List<TopVentaDTO>();
            using var cn = new SQLiteConnection(_conn);
            cn.Open();
            const string sql = @"
                SELECT p.Nombre,
                       SUM(d.Cantidad) AS Cantidad,
                       SUM(d.Subtotal) AS Importe
                FROM DetalleVentas d
                INNER JOIN Ventas v ON d.VentaId = v.Id
                INNER JOIN Productos p ON d.ProductoId = p.Id
                WHERE v.Estado='Activa'
                  AND v.Fecha BETWEEN @Desde AND @Hasta
                GROUP BY p.Nombre
                ORDER BY Cantidad DESC
                LIMIT @Limite;";
            using var cmd = new SQLiteCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Desde", desde);
            cmd.Parameters.AddWithValue("@Hasta", hasta);
            cmd.Parameters.AddWithValue("@Limite", limite);
            using var rd = cmd.ExecuteReader();
            int pos = 1;
            while (rd.Read())
            {
                lista.Add(new TopVentaDTO
                {
                    Pos = pos++,
                    Nombre = rd.GetString(0),
                    Cantidad = rd.GetInt32(1),
                    Importe = "S/ " + rd.GetDecimal(2).ToString("N2"),
                    NombreConCantidad = $"{rd.GetString(0)} ({rd.GetInt32(1)})"
                });
            }
            return lista;
        }

        private void CalcularIndicadoresDiaYMeta(List<Venta> ventas)
        {
            var hoy = DateTime.Today;
            var finHoy = hoy.AddDays(1).AddTicks(-1);

            decimal ventasHoy = ventas.Where(v => v.Estado == "Activa" && v.Fecha >= hoy && v.Fecha <= finHoy)
                                      .Sum(v => v.Total);
            txtVentasHoy.Text = "S/ " + ventasHoy.ToString("N2");

            var ahora = DateTime.Now;
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddTicks(-1);
            decimal ventasMes = ventas.Where(v => v.Estado == "Activa" && v.Fecha >= inicioMes && v.Fecha <= finMes)
                                      .Sum(v => v.Total);
            txtVentasMes.Text = "S/ " + ventasMes.ToString("N2");
            ActualizarUI_Meta(ventasMes, ahora);
        }

        private void ActualizarUI_Meta(decimal ventasMes, DateTime ahora)
        {
            if (_metaVentasMes is null or <= 0)
            {
                pbMetaMes.Value = 0;
                pbMetaMes.Visibility = Visibility.Collapsed;
                panelDetallesMeta.Visibility = Visibility.Collapsed;
                panelMetaValor.Visibility = Visibility.Collapsed;
                btnEditarMeta.Visibility = Visibility.Collapsed;
                txtMetaNoDefinida.Visibility = Visibility.Visible;
                return;
            }

            txtMetaNoDefinida.Visibility = Visibility.Collapsed;
            panelMetaValor.Visibility = Visibility.Visible;
            btnEditarMeta.Visibility = Visibility.Visible;
            txtMetaMesValor.Text = $"Meta: S/ {_metaVentasMes.Value:N2}";

            decimal porcMeta = Math.Min(100m, ventasMes * 100m / _metaVentasMes.Value);
            pbMetaMes.Value = (double)porcMeta;
            pbMetaMes.Visibility = Visibility.Visible;

            txtMetaMesPorc.Text = porcMeta.ToString("N1") + "%";
            txtMetaMesPorc.Foreground =
                porcMeta >= 100 ? Brushes.LightGreen :
                porcMeta >= 70 ? Brushes.Gold :
                                 Brushes.LightGray;

            int diaActual = ahora.Day;
            int diasMes = DateTime.DaysInMonth(ahora.Year, ahora.Month);
            int diasRestantes = diasMes - diaActual;
            decimal promedioDiario = diaActual == 0 ? 0 : ventasMes / diaActual;
            decimal proyeccion = promedioDiario * diasMes;
            decimal faltante = Math.Max(0, _metaVentasMes.Value - ventasMes);
            decimal promedioNecesario = diasRestantes > 0 ? faltante / diasRestantes : 0;

            txtMetaMesDetalle.Text = "Proy: S/ " + proyeccion.ToString("N2");
            txtMetaMesNecesario.Text = "Nec: S/ " + promedioNecesario.ToString("N2") + "/d";
            pbMetaMes.ToolTip =
                $"Meta: S/ {_metaVentasMes.Value:N2}\nAvance: {porcMeta:N1}%\nProm: S/ {promedioDiario:N2}/d\nProy: S/ {proyeccion:N2}\nFaltante: S/ {faltante:N2}\nNecesario: S/ {promedioNecesario:N2}/d";

            panelDetallesMeta.Visibility = Visibility.Visible;
        }
        #endregion

        #region Meta ventana
        private void DefinirMeta_Click(object sender, RoutedEventArgs e)
        {
            var win = new MetaMesWindow(_metaVentasMes);
            if (win.ShowDialog() == true && win.MetaDefinida.HasValue && win.MetaDefinida.Value > 0)
            {
                _metaVentasMes = win.MetaDefinida.Value;
                GuardarMetaEnArchivo();
                MainSnackbar.MessageQueue?.Enqueue("Meta mensual actualizada.");
                var ventas = _ventaService.ObtenerTodas();
                CalcularIndicadoresDiaYMeta(ventas);
            }
        }
        #endregion

        #region Exportación
        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                ExportarVentasResumenCsv();
                return;
            }
            btnExportar.ContextMenu!.PlacementTarget = btnExportar;
            btnExportar.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btnExportar.ContextMenu.IsOpen = true;
        }
        private void BtnExportar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExportarVentasResumenCsv();
                e.Handled = true;
            }
        }
        private void ExportarResumen_Click(object sender, RoutedEventArgs e) => ExportarVentasResumenCsv();
        private void ExportarDetalle_Click(object sender, RoutedEventArgs e) => ExportarVentasDetalleCsv();
        private void ExportarTop_Click(object sender, RoutedEventArgs e) => ExportarTopCsv();

        private (DateTime desde, DateTime hasta)? ObtenerRango()
        {
            var desde = (dpDesde.SelectedDate ?? DateTime.Today).Date;
            var hasta = (dpHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hasta < desde)
            {
                MainSnackbar.MessageQueue?.Enqueue("Rango inválido.");
                return null;
            }
            return (desde, hasta);
        }
        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0;
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
        private SaveFileDialog CrearDialogo(string fileName, string titulo) => new()
        {
            Title = titulo,
            Filter = "CSV (*.csv)|*.csv",
            FileName = fileName,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        private void MostrarErrorExport(string tipo, Exception ex)
        {
            MainSnackbar.MessageQueue?.Enqueue($"Error al exportar {tipo.ToLower()}.");
            MessageBox.Show($"Error ({tipo}): {ex.Message}", "Exportar",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void ExportarVentasResumenCsv()
        {
            try
            {
                var rango = ObtenerRango(); if (rango == null) return;
                var (desde, hasta) = rango.Value;
                var ventas = _ventaService.ObtenerTodas()
                    .Where(v => v.Estado == "Activa" && v.Fecha >= desde && v.Fecha <= hasta)
                    .OrderBy(v => v.Fecha).ToList();
                if (!ventas.Any())
                {
                    MainSnackbar.MessageQueue?.Enqueue("No hay ventas en el rango.");
                    return;
                }
                var dlg = CrearDialogo($"ventas_resumen_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", "Exportar ventas (Resumen)");
                if (dlg.ShowDialog(this) != true)
                {
                    MainSnackbar.MessageQueue?.Enqueue("Exportación cancelada.");
                    return;
                }
                var sb = new StringBuilder().AppendLine("Id;Fecha;Total;Usuario");
                foreach (var v in ventas)
                    sb.AppendLine($"{v.Id};{v.Fecha:yyyy-MM-dd HH:mm:ss};{v.Total:N2};{Csv(v.UsuarioNombre)}");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MainSnackbar.MessageQueue?.Enqueue($"Resumen exportado ({ventas.Count})");
            }
            catch (Exception ex) { MostrarErrorExport("Resumen", ex); }
        }
        private void ExportarVentasDetalleCsv()
        {
            try
            {
                var rango = ObtenerRango(); if (rango == null) return;
                var (desde, hasta) = rango.Value;
                var listado = new List<DetalleLinea>();
                using (var cn = new SQLiteConnection(_conn))
                {
                    cn.Open();
                    const string sql = @"
                        SELECT v.Id, v.Fecha, v.Total, u.Nombre,
                               p.Nombre, d.Cantidad, d.PrecioUnitario, d.Subtotal
                        FROM Ventas v
                        INNER JOIN Usuarios u ON u.Id = v.UsuarioId
                        INNER JOIN DetalleVentas d ON d.VentaId = v.Id
                        INNER JOIN Productos p ON p.Id = d.ProductoId
                        WHERE v.Estado='Activa'
                          AND v.Fecha BETWEEN @Desde AND @Hasta
                        ORDER BY v.Fecha, v.Id, d.Id;";
                    using var cmd = new SQLiteCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Desde", desde);
                    cmd.Parameters.AddWithValue("@Hasta", hasta);
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        listado.Add(new DetalleLinea
                        {
                            VentaId = rd.GetInt32(0),
                            Fecha = rd.GetDateTime(1),
                            TotalVenta = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2)),
                            Usuario = rd.IsDBNull(3) ? "" : rd.GetString(3),
                            Producto = rd.GetString(4),
                            Cantidad = rd.GetInt32(5),
                            PrecioUnit = rd.IsDBNull(6) ? 0m : Convert.ToDecimal(rd.GetValue(6)),
                            Subtotal = rd.IsDBNull(7) ? 0m : Convert.ToDecimal(rd.GetValue(7))
                        });
                    }
                }
                if (!listado.Any())
                {
                    MainSnackbar.MessageQueue?.Enqueue("No hay ventas con detalle.");
                    return;
                }
                var dlg = CrearDialogo($"ventas_detalle_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", "Exportar ventas (Detalle)");
                if (dlg.ShowDialog(this) != true)
                {
                    MainSnackbar.MessageQueue?.Enqueue("Exportación cancelada.");
                    return;
                }
                var sb = new StringBuilder().AppendLine("VentaId;Fecha;Usuario;TotalVenta;Producto;Cantidad;PrecioUnitario;Subtotal");
                foreach (var d in listado)
                {
                    sb.AppendLine(string.Join(";",
                        d.VentaId,
                        d.Fecha.ToString("yyyy-MM-dd HH:mm:ss"),
                        Csv(d.Usuario),
                        d.TotalVenta.ToString("N2"),
                        Csv(d.Producto),
                        d.Cantidad,
                        d.PrecioUnit.ToString("N2"),
                        d.Subtotal.ToString("N2")));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MainSnackbar.MessageQueue?.Enqueue($"Detalle exportado ({listado.Count} filas)");
            }
            catch (Exception ex) { MostrarErrorExport("Detalle", ex); }
        }
        private void ExportarTopCsv()
        {
            try
            {
                var rango = ObtenerRango(); if (rango == null) return;
                var (desde, hasta) = rango.Value;
                var top = ObtenerTopVendidos(desde, hasta, 5);
                if (!top.Any())
                {
                    MainSnackbar.MessageQueue?.Enqueue("Sin Top 5 en el rango.");
                    return;
                }
                var dlg = CrearDialogo($"top5_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", "Exportar Top 5 productos");
                if (dlg.ShowDialog(this) != true)
                {
                    MainSnackbar.MessageQueue?.Enqueue("Exportación cancelada.");
                    return;
                }
                var sb = new StringBuilder().AppendLine("Pos;Producto;Cantidad;Importe");
                foreach (var t in top)
                    sb.AppendLine($"{t.Pos};{Csv(t.Nombre)};{t.Cantidad};{t.Importe.Replace("S/ ", "")}");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MainSnackbar.MessageQueue?.Enqueue("Top 5 exportado");
            }
            catch (Exception ex) { MostrarErrorExport("Top 5", ex); }
        }
        #endregion

        #region UI
        private void MostrarOverlay(bool visible) =>
            LoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        private async void BtnRefrescarDashboard_Click(object sender, RoutedEventArgs e) => await CargarTodoAsync();
        private async void BtnAplicarFiltros_Click(object sender, RoutedEventArgs e) => await CargarTodoAsync();
        private async void FiltroFecha_Changed(object sender, EventArgs e) => await CargarTodoAsync();
        private void CardProductos_Click(object sender, MouseButtonEventArgs e) => new ProductoWindow().ShowDialog();
        private void CardVentas_Click(object sender, MouseButtonEventArgs e) => new VentaWindow().ShowDialog();
        private void CardCompras_Click(object sender, MouseButtonEventArgs e) => new CompraWindow().ShowDialog();
        private void CardStockBajo_Click(object sender, MouseButtonEventArgs e)
        {
            if (dgStockBajo.Items.Count == 0)
                MainSnackbar.MessageQueue?.Enqueue("No hay productos con stock bajo.");
            else
                MainSnackbar.MessageQueue?.Enqueue("Listado visible.");
        }
        #endregion

        #region Layout Reordenamiento
        private void ReordenarSegunLayout()
        {
            try
            {
                if (!File.Exists(_layoutConfigPath)) return;
                var json = File.ReadAllText(_layoutConfigPath);
                var order = JsonSerializer.Deserialize<List<string>>(json);
                if (order is null || order.Count == 0) return;
                AplicarOrden(order);
            }
            catch { }
        }
        private void CapturarOrdenPorDefecto()
        {
            if (_defaultOrder != null && _defaultOrder.Count > 0) return;
            if (MetricsPanel == null || MetricsPanel.Children.Count == 0) return;
            _defaultOrder = MetricsPanel.Children
                .OfType<FrameworkElement>()
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }
        private void AplicarOrden(IEnumerable<string> order)
        {
            var list = order?.ToList();
            if (list == null || list.Count == 0) return;
            var current = MetricsPanel.Children.Cast<UIElement>().ToList();
            MetricsPanel.Children.Clear();
            foreach (var name in list)
            {
                var card = current.FirstOrDefault(c => (c as FrameworkElement)?.Name == name);
                if (card != null)
                {
                    current.Remove(card);
                    MetricsPanel.Children.Add(card);
                }
            }
            foreach (var leftover in current) MetricsPanel.Children.Add(leftover);
        }
        private void GuardarLayoutDesdeLista(List<string> orden)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_layoutConfigPath)!);
                File.WriteAllText(_layoutConfigPath,
                    JsonSerializer.Serialize(orden, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
        private void BtnLayout_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var miReset = new MenuItem { Header = "Restablecer orden (por defecto)" };
            miReset.Click += (_, _) =>
            {
                CapturarOrdenPorDefecto();
                try { if (File.Exists(_layoutConfigPath)) File.Delete(_layoutConfigPath); } catch { }
                GuardarLayoutDesdeLista(_defaultOrder ?? new());
                MainSnackbar.MessageQueue?.Enqueue("Orden por defecto restaurado.");
            };
            var miGuardar = new MenuItem { Header = "Guardar orden actual" };
            miGuardar.Click += (_, _) =>
            {
                var actual = MetricsPanel.Children.OfType<FrameworkElement>()
                    .Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                GuardarLayoutDesdeLista(actual);
                MainSnackbar.MessageQueue?.Enqueue("Orden guardado.");
            };
            var miLock = new MenuItem { Header = _layoutLocked ? "Desbloquear reordenamiento" : "Bloquear reordenamiento" };
            miLock.Click += (_, _) =>
            {
                _layoutLocked = !_layoutLocked;
                MainSnackbar.MessageQueue?.Enqueue(_layoutLocked ? "Reordenamiento bloqueado." : "Reordenamiento habilitado.");
            };
            menu.Items.Add(miReset);
            menu.Items.Add(miGuardar);
            menu.Items.Add(new Separator());
            menu.Items.Add(miLock);
            menu.IsOpen = true;
        }
        private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_layoutLocked) return;
            _dragStartPoint = e.GetPosition(this);
            _dragSourceCard = FindCardRoot(sender as DependencyObject);
        }
        private void Card_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_layoutLocked) return;
            if (_dragSourceCard == null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) < 6 && Math.Abs(pos.Y - _dragStartPoint.Y) < 6) return;
            DragDrop.DoDragDrop(_dragSourceCard, new DataObject(typeof(FrameworkElement), _dragSourceCard), DragDropEffects.Move);
        }
        private void MetricsPanel_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (_layoutLocked) return;
            if (e.Data.GetDataPresent(typeof(FrameworkElement)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }
        private void MetricsPanel_Drop(object sender, DragEventArgs e)
        {
            if (_layoutLocked) return;
            if (!e.Data.GetDataPresent(typeof(FrameworkElement))) return;

            var source = (FrameworkElement)e.Data.GetData(typeof(FrameworkElement))!;
            var target = FindCardRoot(e.OriginalSource as DependencyObject);
            int sIndex = MetricsPanel.Children.IndexOf(source);
            if (sIndex < 0) return;

            int insertIndex;
            if (target != null && target != source)
            {
                int tIndex = MetricsPanel.Children.IndexOf(target);
                insertIndex = tIndex;
                if (sIndex < tIndex) insertIndex--;
            }
            else
            {
                var pt = e.GetPosition(MetricsPanel);
                insertIndex = MetricsPanel.Children.Count;
                for (int i = 0; i < MetricsPanel.Children.Count; i++)
                {
                    if (MetricsPanel.Children[i] is FrameworkElement fe)
                    {
                        var origin = fe.TranslatePoint(new Point(0, 0), MetricsPanel);
                        var medioX = origin.X + fe.ActualWidth / 2;
                        if (pt.Y < origin.Y + fe.ActualHeight && pt.X < medioX)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                }
                if (insertIndex > sIndex) insertIndex--;
            }
            if (insertIndex == sIndex || insertIndex < 0) { _dragSourceCard = null; return; }

            MetricsPanel.Children.RemoveAt(sIndex);
            MetricsPanel.Children.Insert(insertIndex, source);
            var nuevo = MetricsPanel.Children
                .OfType<FrameworkElement>()
                .Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            GuardarLayoutDesdeLista(nuevo);
            _dragSourceCard = null;
        }
        private FrameworkElement? FindCardRoot(DependencyObject? start)
        {
            while (start != null)
            {
                if (start is MaterialDesignThemes.Wpf.Card card && MetricsPanel.Children.Contains(card))
                    return card;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }
        #endregion

        #region Compacto
        private void BtnCompacto_Click(object sender, RoutedEventArgs e) => ToggleCompacto();
        private void ToggleCompacto()
        {
            _modoCompacto = !_modoCompacto;
            foreach (var card in MetricsPanel.Children.OfType<MaterialDesignThemes.Wpf.Card>())
            {
                card.MinHeight = _modoCompacto ? 110 : 140;
                if (card.Content is Panel panel)
                {
                    foreach (var tb in panel.Children.OfType<TextBlock>().Where(t => t.FontSize == 11))
                        tb.Visibility = _modoCompacto ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            btnCompacto.Content = _modoCompacto ? "Detallado" : "Compacto";
            MainSnackbar.MessageQueue?.Enqueue(_modoCompacto ? "Modo compacto" : "Modo detallado");
        }
        #endregion

        #region Apariencia / Animación
        private void AplicarColoresSemanticos()
        {
            if (decimal.TryParse(txtPorcStockBajo.Text.Replace("%", ""), out var sb))
            {
                txtPorcStockBajo.Foreground =
                    sb < 10 ? (Brush)FindResource("ColorSuccess") :
                    sb < 25 ? (Brush)FindResource("ColorWarn") :
                              (Brush)FindResource("ColorDanger");
            }
        }
        private void AnimarAparicionTarjetas()
        {
            int delay = 0;
            foreach (FrameworkElement fe in MetricsPanel.Children)
            {
                fe.Opacity = 0;
                var trans = new TranslateTransform { Y = 10 };
                fe.RenderTransform = trans;
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
                var move = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
                fe.BeginAnimation(OpacityProperty, fade);
                trans.BeginAnimation(TranslateTransform.YProperty, move);
                delay += 40;
            }
        }
        private void ActualizarSparklineUltimos7Dias(List<Venta> ventas)
        {
            if (sparkVentas7 == null) return;
            var hoy = DateTime.Today;
            var dias = Enumerable.Range(0, 7).Select(i => hoy.AddDays(-6 + i)).ToList();
            var datos = dias.Select(d => ventas.Where(v => v.Estado == "Activa" && v.Fecha.Date == d).Sum(v => v.Total)).ToList();
            var max = datos.Max();
            if (max <= 0) max = 1;
            var sb = new StringBuilder();
            for (int i = 0; i < datos.Count; i++)
            {
                double x = i * (120.0 / (datos.Count - 1));
                double y = 20 - (double)datos[i] / (double)max * 20;
                sb.Append($"{x:F1},{y + 4:F1} ");
            }
            sparkVentas7.Points = PointCollection.Parse(sb.ToString());
        }
        #endregion

        #region Acciones rápidas
        private void BtnAgregarAccion_Click(object sender, RoutedEventArgs e)
        {
            if (cbAccion.SelectedValue is string clave && AgregarAccionSiNoExiste(clave))
            {
                GuardarAcciones();
                cbAccion.SelectedIndex = -1;
            }
        }
        private bool AgregarAccionSiNoExiste(string clave)
        {
            if (_accionesRapidas.Any(a => a.Clave == clave)) return false;
            var def = _catalogoAcciones.FirstOrDefault(x => x.Clave == clave);
            if (def == null) return false;
            _accionesRapidas.Add(BuildQuickAction(def));
            return true;
        }
        private QuickAction BuildQuickAction(AccionDef def) =>
            new()
            {
                Clave = def.Clave,
                Titulo = def.Titulo,
                Icono = def.Icono,
                Ejecutar = def.Clave switch
                {
                    "NuevaVenta" => () => { new VentaWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "NuevaCompra" => () => { new CompraWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "AgregarProducto" => () =>
                    {
                        var w = new ProductoEdicionWindow(_conn);
                        if (w.ShowDialog() == true)
                        {
                            try { _productoService.AgregarProducto(w.Producto); } catch { }
                        }
                        _ = CargarTodoAsync();
                    }
                    ,
                    "NuevoProveedor" => () => { new ProveedorWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Productos" => () => { new ProductoWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Ventas" => () => { new HistorialVentasWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Compras" => () => { new HistorialComprasWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Proveedores" => () => { new ProveedorWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Usuarios" => () => { new UsuarioWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    "Categorias" => () => { new CategoriaWindow().ShowDialog(); _ = CargarTodoAsync(); }
                    ,
                    _ => () => { }
                }
            };
        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is QuickAction qa)
                qa.Ejecutar?.Invoke();
        }
        private void QuickAction_Quitar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickAction qa)
            {
                _accionesRapidas.Remove(qa);
                GuardarAcciones();
            }
        }
        private bool CargarAccionesGuardadas()
        {
            try
            {
                if (!File.Exists(QuickActionsFilePath)) return false;
                var claves = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(QuickActionsFilePath)) ?? new();
                bool added = false;
                foreach (var c in claves) added |= AgregarAccionSiNoExiste(c);
                return added || claves.Count > 0;
            }
            catch { return false; }
        }
        private void GuardarAcciones()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QuickActionsFilePath)!);
                var json = JsonSerializer.Serialize(_accionesRapidas.Select(a => a.Clave).ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(QuickActionsFilePath, json);
            }
            catch { }
        }
        private record AccionDef(string Clave, string Titulo, string Icono);
        private class QuickAction
        {
            public string Clave { get; set; } = "";
            public string Titulo { get; set; } = "";
            public string Icono { get; set; } = "\uE10F";
            public Action? Ejecutar { get; set; }
        }
        #endregion

        #region DTO
        private class TopVentaDTO
        {
            public int Pos { get; set; }
            public string Nombre { get; set; } = "";
            public int Cantidad { get; set; }
            public string Importe { get; set; } = "";
            public string NombreConCantidad { get; set; } = "";
        }
        private class DetalleLinea
        {
            public int VentaId { get; set; }
            public DateTime Fecha { get; set; }
            public decimal TotalVenta { get; set; }
            public string Usuario { get; set; } = "";
            public string Producto { get; set; } = "";
            public int Cantidad { get; set; }
            public decimal PrecioUnit { get; set; }
            public decimal Subtotal { get; set; }
        }
        #endregion

        private int ObtenerUnidadesVendidas(DateTime desde, DateTime hasta)
        {
            using var cn = new SQLiteConnection(_conn);
            cn.Open();
            const string sql = @"
                SELECT IFNULL(SUM(d.Cantidad),0)
                FROM DetalleVentas d
                INNER JOIN Ventas v ON v.Id = d.VentaId
                WHERE v.Estado='Activa'
                  AND v.Fecha BETWEEN @Desde AND @Hasta;";
            using var cmd = new SQLiteCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Desde", desde);
            cmd.Parameters.AddWithValue("@Hasta", hasta);
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result == DBNull.Value ? 0 : result);
        }
    }
}