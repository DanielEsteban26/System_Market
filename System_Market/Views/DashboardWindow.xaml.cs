using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private readonly CategoriaService _categoriaService;
        private readonly UsuarioService _usuarioService;

        private readonly string _usuarioActualNombre;
        private int _umbralStock = 5;

        private readonly ObservableCollection<QuickAction> _accionesRapidas = new();
        public ObservableCollection<RangoFechaPreset> RangoFechaPresets { get; } = new();
        public ObservableCollection<OrdenRecienteDTO> OrdenesRecientes { get; } = new();

        private readonly List<AccionDef> _catalogoAcciones = new()
        {
            new("NuevaVenta","Nueva Venta","\uE73E"),
            new("NuevaCompra","Nueva Compra","\uE73E"),
            new("AgregarProducto","Agregar Producto","\uE710"),
            new("NuevoProveedor","Proveedor Nuevo","\uE716"),
            new("Productos","Listado Productos","\uE14C"),
            new("Ventas","Listado Ventas","\uE7BF"),
            new("Compras","Listado Compras","\uE73E"),
            new("Proveedores","Listado Proveedores","\uE716"),
            new("Usuarios","Gestión Usuarios","\uE77B"),
            new("Categorias","Gestión Categorías","\uE1CB")
        };

        public int MaxCantidadTop { get; private set; }

        private string QuickActionsFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "System_Market", "quick_actions_dashboard.json");

        private ColumnSeries<ObservablePoint>? _serieIngresos;
        private ColumnSeries<ObservablePoint>? _serieGastos;
        private readonly ObservablePoint _pIngresos = new(0, 0);
        private readonly ObservablePoint _pGastos = new(1, 0);

        private Dictionary<(int h,int d),(int ventas,int rank,double pctMax)> _heatmapMeta
    = new();

        private DateTime _ultimoDialogo = DateTime.MinValue;
        private string? _ultimoCodigo;

        public DashboardWindow(string usuarioActualNombre = "Usuario")
        {
            _usuarioActualNombre = string.IsNullOrWhiteSpace(usuarioActualNombre) ? "Usuario" : usuarioActualNombre;
            InitializeComponent();
            DataContext = this;

            _conn = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(_conn);
            _ventaService = new VentaService(_conn);
            _compra_service: _compraService = new CompraService(_conn); // placeholder to keep style
            _compraService = new CompraService(_conn);
            _categoriaService = new CategoriaService(_conn);
            _usuarioService = new UsuarioService(_conn);

            MainSnackbar.MessageQueue ??= new SnackbarMessageQueue(TimeSpan.FromSeconds(4));

            icQuickActions.ItemsSource = _accionesRapidas;
            cbAccion.ItemsSource = _catalogoAcciones;

            CargarPresetsRangoFecha();
            CargarCategorias();
            CargarSucursalesPlaceholder(); // ahora carga usuarios en el combo de sucursal

            if (!CargarAccionesGuardadas())
            {
                AgregarAccionSiNoExiste("NuevaVenta");
                AgregarAccionSiNoExiste("AgregarProducto");
                AgregarAccionSiNoExiste("NuevoProveedor");
                GuardarAcciones();
            }

            Closed += (_, __) => GuardarAcciones();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Preferir el usuario de la sesión si está disponible, si no usar el valor pasado al constructor
            var nombreSesion = System_Market.Models.SesionActual.Usuario?.Nombre;
            txtUsuarioActual.Text = !string.IsNullOrWhiteSpace(nombreSesion) ? nombreSesion : _usuarioActualNombre;

            BarcodeScannerService.Start();
            AplicarPresetSeleccionado(false);
            await CargarTodoAsync();
        }

        #region Filtros
        private async void BtnRefrescarDashboard_Click(object sender, RoutedEventArgs e) => await CargarTodoAsync();
        private async void BtnAplicarFiltros_Click(object sender, RoutedEventArgs e) => await CargarTodoAsync();
        private async void FiltroFecha_Changed(object sender, EventArgs e)
        {
            if (cbRangoFecha.SelectedValue as string == "PERS")
                await CargarTodoAsync();
        }
        private async void FiltroRangoFecha_Changed(object sender, SelectionChangedEventArgs e)
        {
            AplicarPresetSeleccionado(true);
            if (cbRangoFecha.SelectedValue as string != "PERS")
                await CargarTodoAsync();
        }
        private async void FiltroCategoria_Changed(object sender, SelectionChangedEventArgs e) => await CargarTodoAsync();
        private async void FiltroSucursal_Changed(object sender, SelectionChangedEventArgs e) => await CargarTodoAsync();

        private void CargarPresetsRangoFecha()
        {
            RangoFechaPresets.Clear();
            RangoFechaPresets.Add(new("ULT7", "Últimos 7 días", () =>
            {
                var h = DateTime.Today;
                var d = h.AddDays(-6);
                return (d, h.AddDays(1).AddTicks(-1));
            }));
            RangoFechaPresets.Add(new("HOY", "Hoy", () =>
            {
                var d = DateTime.Today;
                return (d, d.AddDays(1).AddTicks(-1));
            }));
            RangoFechaPresets.Add(new("AYER", "Ayer", () =>
            {
                var d = DateTime.Today.AddDays(-1);
                return (d, d.AddDays(1).AddTicks(-1));
            }));
            RangoFechaPresets.Add(new("MES", "Este mes", () =>
            {
                var d = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                return (d, d.AddMonths(1).AddTicks(-1));
            }));
            RangoFechaPresets.Add(new("PERS", "Personalizado", () =>
            {
                var d = (dpDesde.SelectedDate ?? DateTime.Today).Date;
                var h = (dpHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                return (d, h);
            }));
            cbRangoFecha.SelectedValue = "ULT7";
        }

        private void AplicarPresetSeleccionado(bool actualizarPickers)
        {
            if (cbRangoFecha.SelectedItem is not RangoFechaPreset preset) return;
            if (preset.Clave == "PERS")
            {
                dpDesde.IsEnabled = dpHasta.IsEnabled = true; return;
            }
            dpDesde.IsEnabled = dpHasta.IsEnabled = false;
            var (d, h) = preset.Rango();
            if (actualizarPickers)
            {
                dpDesde.SelectedDate = d;
                dpHasta.SelectedDate = h.Date;
            }
        }
        #endregion

        private async Task CargarTodoAsync()
        {
            try
            {
                MostrarOverlay(true);
                var (desde, hasta) = ObtenerRangoFechas();
                if (!int.TryParse(txtUmbralStock.Text.Trim(), out _umbralStock) || _umbralStock < 0)
                    _umbralStock = 5;

                int? categoriaId = ObtenerCategoriaSeleccionada();
                int? usuarioId = ObtenerUsuarioSeleccionada();

                var productosTask = Task.Run(_productoService.ObtenerTodos);
                var ventasTask = Task.Run(_ventaService.ObtenerTodas);
                var comprasTask = Task.Run(_compraService.ObtenerTodas);
                await Task.WhenAll(productosTask, ventasTask, comprasTask);

                var productos = productosTask.Result;
                var ventasTodas = ventasTask.Result.Where(v => v.Estado == "Activa").ToList();
                var comprasTodas = comprasTask.Result.Where(c => c.Estado == "Activa").ToList();

                if (categoriaId is > 0)
                {
                    var ventaIds = ObtenerVentaIdsPorCategoria(categoriaId.Value);
                    ventasTodas = ventasTodas.Where(v => ventaIds.Contains(v.Id)).ToList();

                    var compraIds = ObtenerCompraIdsPorCategoria(categoriaId.Value);
                    comprasTodas = comprasTodas.Where(c => compraIds.Contains(c.Id)).ToList();

                    productos = productos.Where(p => p.CategoriaId == categoriaId.Value).ToList();
                }

                if (usuarioId is > 0)
                {
                    ventasTodas = ventasTodas.Where(v => v.UsuarioId == usuarioId.Value).ToList();
                    comprasTodas = comprasTodas.Where(c => c.UsuarioId == usuarioId.Value).ToList();
                }

                var ventasRango = ventasTodas.Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                    .OrderBy(v => v.Fecha).ToList();
                var comprasRango = comprasTodas.Where(c => c.Fecha >= desde && c.Fecha <= hasta).ToList();

                decimal totalVentasRango = ventasRango.Sum(v => v.Total);
                decimal totalComprasRango = comprasRango.Sum(c => c.Total);
                decimal ganancia = totalVentasRango - totalComprasRango;

                txtVentasRango.Text = CurrencyService.FormatSoles(totalVentasRango, "N2");
                txtGananciaNeta.Text = CurrencyService.FormatSoles(ganancia, "N2");
                txtGananciaInfo.Text = "Compras: " + CurrencyService.FormatSoles(totalComprasRango, "N2");
                txtCantidadVentas.Text = ventasRango.Count.ToString("N0", CultureInfo.CurrentCulture);

                txtIngresosMonto.Text = CurrencyService.FormatSoles(totalVentasRango, "N2");
                txtGastosMonto.Text = CurrencyService.FormatSoles(totalComprasRango, "N2");

                int diasRango = Math.Max(1, (hasta.Date - desde.Date).Days + 1);
                double objetivoOrdenes = diasRango * 5;
                pbOrdenes.Maximum = objetivoOrdenes;
                pbOrdenes.Value = ventasRango.Count;
                pbOrdenes.ToolTip = string.Format(CultureInfo.CurrentCulture, "Órdenes: {0:N0} / {1:N0}", ventasRango.Count, objetivoOrdenes);

                var stockBajo = productos.Where(p => p.Stock <= _umbralStock).OrderBy(p => p.Stock).ToList();
                txtStockBajo.Text = stockBajo.Count.ToString();
                dgStockBajo.ItemsSource = stockBajo;
                txtPorcStockBajo.Text = productos.Count == 0
                    ? "0%"
                    : (stockBajo.Count * 100m / productos.Count).ToString("N1") + "%";
                AplicarColorStock();

                var hoy = DateTime.Today;
                decimal ventasHoy = ventasTodas.Where(v => v.Fecha.Date == hoy).Sum(v => v.Total);
                txtCantidadVentas.ToolTip = "Ventas hoy: " + CurrencyService.FormatSoles(ventasHoy, "N2");

                CalcularMetaYProyeccion(ventasTodas);

                var top = ObtenerTopVendidos(desde, hasta, 10, categoriaId, usuarioId);
                MaxCantidadTop = top.Any() ? top.Max(t => t.Cantidad) : 1;
                lvTopVendidos.ItemsSource = top;

                ConstruirOrdenesRecientes(ventasTodas);
                ConstruirEvolucionVentas(ventasRango, desde, hasta);
                ConstruirDistribucionCategorias(desde, hasta, categoriaId);
                ConstruirIngresosGastos(totalVentasRango, totalComprasRango);
                CalcularDeltaVentas(ventasTodas, desde, hasta, totalVentasRango);
                ConstruirHeatmapHoras(ventasRango);

                if (stockBajo.Count > 0)
                    MainSnackbar.MessageQueue?.Enqueue($"{stockBajo.Count} prod. con stock ≤ {_umbralStock}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar dashboard: " + ex.Message, "Dashboard",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { MostrarOverlay(false); }
        }

        private void ConstruirOrdenesRecientes(List<Venta> ventasTodas)
        {
            OrdenesRecientes.Clear();
            foreach (var v in ventasTodas.OrderByDescending(v => v.Fecha).Take(25))
            {
                string cliente =
                    (v.GetType().GetProperty("ClienteNombre")?.GetValue(v) as string)?.Trim()
                    ?? (v.UsuarioNombre ?? "");
                OrdenesRecientes.Add(new OrdenRecienteDTO
                {
                    Fecha = FormatearFechaCorta(v.Fecha),
                    Cliente = string.IsNullOrWhiteSpace(cliente) ? "-" : cliente,
                    Estado = v.Estado ?? "",
                    Monto = v.Total
                });
            }
        }

        private static string FormatearFechaCorta(DateTime fecha)
        {
            var ci = CultureInfo.GetCultureInfo("es-ES");
            var txt = fecha.ToString("MMM d", ci).Replace(".", "");
            return char.ToUpper(txt[0]) + txt[1..];
        }

        #region Visualizaciones
        private void ConstruirEvolucionVentas(List<Venta> ventasRango, DateTime desde, DateTime hasta)
        {
            var dias = Enumerable.Range(0, (hasta.Date - desde.Date).Days + 1)
                .Select(i => desde.Date.AddDays(i)).ToList();

            var totales = dias
                .Select(d => (double)ventasRango.Where(v => v.Fecha.Date == d).Sum(v => v.Total))
                .ToList();

            double maxValor = totales.Count == 0 ? 0 : totales.Max();
            double maxAjustado = maxValor == 0 ? 1 : AjustarEscala(maxValor);

            var linea = new LineSeries<double>
            {
                Values = totales,
                Fill = null,
                Stroke = new SolidColorPaint(new SKColor(0x3F, 0xC1, 0xFF)) { StrokeThickness = 2 },
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.35
            };

            chartEvolucionVentas.Series = new ISeries[] { linea };
            chartEvolucionVentas.XAxes = new[]
            {
                new Axis
                {
                    Labels = dias.Select(d => d.ToString("dd/MM")).ToArray(),
                    TextSize = 12,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(35,55,60)){ StrokeThickness = 0.5f }
                }
            };
            chartEvolucionVentas.YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = maxAjustado,
                    Labeler = v => "S/ " + v.ToString("N0"),
                    TextSize = 12,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(35,55,60)){ StrokeThickness = 0.5f }
                }
            };
        }

        private static double AjustarEscala(double max)
        {
            if (max <= 0) return 1;
            double potencia = Math.Pow(10, Math.Floor(Math.Log10(max)));
            double mantisa = max / potencia;
            double escala = mantisa <= 1 ? 1 :
                            mantisa <= 2 ? 2 :
                            mantisa <= 5 ? 5 : 10;
            return escala * potencia;
        }

        private void ConstruirDistribucionCategorias(DateTime desde, DateTime hasta, int? categoriaFiltro)
        {
            var cantidades = new List<(string Nombre, int Cant)>();
            using var cn = new SQLiteConnection(_conn);
            cn.Open();

            var sql = @"
                SELECT IFNULL(c.Nombre,'(Sin categoría)') AS NomCat, SUM(d.Cantidad) AS Cant
                FROM DetalleVentas d
                INNER JOIN Ventas v ON v.Id = d.VentaId
                INNER JOIN Productos p ON p.Id = d.ProductoId
                LEFT JOIN Categorias c ON c.Id = p.CategoriaId
                WHERE v.Estado='Activa' 
                  AND v.Fecha BETWEEN @D AND @H
                  {CAT_FILTRO}
                GROUP BY c.Nombre
                ORDER BY Cant DESC;";

            if (categoriaFiltro is > 0)
                sql = sql.Replace("{CAT_FILTRO}", "AND p.CategoriaId=@Cat");
            else
                sql = sql.Replace("{CAT_FILTRO}", "");

            using (var cmd = new SQLiteCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@D", desde);
                cmd.Parameters.AddWithValue("@H", hasta);
                if (categoriaFiltro is > 0)
                    cmd.Parameters.AddWithValue("@Cat", categoriaFiltro.Value);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var nom = rd.GetString(0);
                    var cant = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                    if (cant > 0) cantidades.Add((nom, cant));
                }
            }

            if (cantidades.Count == 0)
            {
                chartDistribucionVentas.Series = Array.Empty<ISeries>();
                return;
            }

            int total = cantidades.Sum(c => c.Cant);
            var palette = new SKColor[]
            {
                new (0xE9,0x1E,0x63),
                new (0x03,0xA9,0xF4),
                new (0x8B,0xC3,0x4A),
                new (0xFF,0xB3,0x4C),
                new (0xAB,0x47,0xBC),
                new (0xFF,0x57,0x22),
                new (0x26,0xA6,0x53),
                new (0x29,0x79,0xFF)
            };

            var series = new List<PieSeries<int>>();
            for (int i = 0; i < cantidades.Count; i++)
            {
                var (nombre, cant) = cantidades[i];
                var color = palette[i % palette.Length];
                bool unico = cantidades.Count == 1;

                series.Add(new PieSeries<int>
                {
                    Name = nombre,
                    Values = new[] { cant },
                    DataLabelsFormatter = p => unico ? $"{cant:N0}" : $"{(double)cant * 100 / total:0.#}%",
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    Fill = new SolidColorPaint(color),
                    Stroke = null
                });
            }

            chartDistribucionVentas.Series = series;
            chartDistribucionVentas.LegendPosition = LiveChartsCore.Measure.LegendPosition.Right;
            chartDistribucionVentas.LegendTextPaint = new SolidColorPaint(new SKColor(210, 220, 228));
            chartDistribucionVentas.Background = null;
        }

        private void ConstruirIngresosGastos(decimal ventas, decimal compras)
        {
            _pIngresos.Y = (double)ventas;
            _pGastos.Y = (double)compras;

            if (_serieIngresos == null || _serieGastos == null)
            {
                _serieIngresos = new ColumnSeries<ObservablePoint>
                {
                    Name = "Ingresos",
                    Values = new[] { _pIngresos },
                    Fill = new SolidColorPaint(new SKColor(0x4D, 0xA3, 0xFF)),
                    Stroke = null,
                    MaxBarWidth = 56
                };

                _serieGastos = new ColumnSeries<ObservablePoint>
                {
                    Name = "Gastos",
                    Values = new[] { _pGastos },
                    Fill = new SolidColorPaint(new SKColor(0xFF, 0x8A, 0x65)),
                    Stroke = null,
                    MaxBarWidth = 56
                };

                chartIngresosGastos.Series = new ISeries[] { _serieIngresos, _serieGastos };

                chartIngresosGastos.XAxes = new[]
                {
                    new Axis
                    {
                        MinLimit = -0.5,
                        MaxLimit = 1.5,
                        Labels = new[] { "Ingresos", "Gastos" },
                        LabelsPaint = new SolidColorPaint(new SKColor(180,190,198)),
                        SeparatorsPaint = null,
                        TextSize = 13
                    }
                };
                chartIngresosGastos.YAxes = new[]
                {
                    new Axis
                    {
                        MinLimit = 0,
                        Labeler = v => "S/ " + v.ToString("N0"),
                        LabelsPaint = new SolidColorPaint(new SKColor(130,140,148)),
                        SeparatorsPaint = new SolidColorPaint(new SKColor(40,60,66)){ StrokeThickness = 0.6f },
                        TextSize = 11
                    }
                };

                chartIngresosGastos.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;
                chartIngresosGastos.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;
                chartIngresosGastos.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None;
                chartIngresosGastos.DrawMarginFrame = null;
            }

            txtIngresosMonto.Text = "S/ " + ventas.ToString("N2");
            txtGastosMonto.Text = "S/ " + compras.ToString("N2");
        }

        // Sucursal por defecto = "Todas"
        private void CargarSucursalesPlaceholder()
        {
            try
            {
                var usuarios = _usuarioService.ObtenerTodos()
                    .OrderBy(u => u.Nombre)
                    .Select(u => new { Id = u.Id, Nombre = u.Nombre })
                    .ToList();

                cbSucursal.ItemsSource = new List<object> { new { Id = 0, Nombre = "Todas" } }
                    .Concat(usuarios)
                    .ToList();

                cbSucursal.SelectedValue = 0;
            }
            catch
            {
                cbSucursal.ItemsSource = new List<object> { new { Id = 0, Nombre = "Todas" } };
                cbSucursal.SelectedValue = 0;
            }
        }

        private static readonly string[] __DAYS_ORDER = { "Lunes","Martes","Miércoles","Jueves","Viernes","Sábado","Domingo" };

private static IEnumerable<LvcColor> BuildGradientPalette()
{
    // Azul → Cian → Verde → Amarillo → Naranja → Rojo
    yield return new LvcColor(0x0D,0x47,0x7A,0xFF);
    yield return new LvcColor(0x08,0x6F,0xA6,0xFF);
    yield return new LvcColor(0x18,0x98,0xB9,0xFF);
    yield return new LvcColor(0x2E,0xB6,0x9F,0xFF);
    yield return new LvcColor(0xD4,0xC9,0x3D,0xFF);
    yield return new LvcColor(0xF2,0x94,0x3C,0xFF);
    yield return new LvcColor(0xE5,0x57,0x3A,0xFF);
    yield return new LvcColor(0xC8,0x1E,0x1E,0xFF);
}

// Reemplaza SOLO el método ConstruirHeatmapHoras existente por este:
private void ConstruirHeatmapHoras(List<Venta> ventasRango)
{
    if (ventasRango.Count == 0)
    {
        chartHorasVentas.Series = Array.Empty<ISeries>();
        txtHorasHint.Visibility = Visibility.Visible;
        return;
    }
    txtHorasHint.Visibility = Visibility.Collapsed;

    static int MapDay(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => 0,
        DayOfWeek.Tuesday => 1,
        DayOfWeek.Wednesday => 2,
        DayOfWeek.Thursday => 3,
        DayOfWeek.Friday => 4,
        DayOfWeek.Saturday => 5,
        DayOfWeek.Sunday => 6,
        _ => 0
    };

    var counts = new int[7,24];
    foreach (var v in ventasRango)
        counts[MapDay(v.Fecha.DayOfWeek), v.Fecha.Hour]++;

    int max = 0;
    for (int d = 0; d < 7; d++)
        for (int h = 0; h < 24; h++)
            if (counts[d,h] > max) max = counts[d,h];

    var puntos = new List<WeightedPoint>(7*24);
    for (int d = 0; d < 7; d++)
        for (int h = 0; h < 24; h++)
            puntos.Add(new WeightedPoint(h, d, counts[d,h]));

    // Ranking para tooltip (lo guardamos en diccionario)
    var conVentas = puntos.Where(p => p.Weight > 0)
                          .OrderByDescending(p => p.Weight)
                          .ThenBy(p => p.Y).ThenBy(p => p.X)
                          .ToList();
    var rank = new Dictionary<(int h,int d), int>();
    for (int i = 0; i < conVentas.Count; i++)
        rank[((int)conVentas[i].X,(int)conVentas[i].Y)] = i + 1;

    // Mostramos solo el máximo como etiqueta para reducir ruido
    int labelThreshold = max;

    var serie = new HeatSeries<WeightedPoint>
    {
        Values = puntos,
        HeatMap = BuildGradientPalette().ToArray(),
        DataLabelsPaint = new SolidColorPaint(SKColors.White),
        DataLabelsFormatter = cp =>
        {
            if (cp.Model is not WeightedPoint w || w.Weight <= 0) return string.Empty;
            if (w.Weight >= labelThreshold)
                return Convert.ToString(w.Weight, CultureInfo.InvariantCulture);
            return string.Empty;
        }
        // Sin TooltipLabelFormatter (no existe en tu versión)
    };

    chartHorasVentas.Series = new ISeries[] { serie };

    // Tooltip básico (posición arriba). Si quieres ocultarlo: TooltipPosition.Hidden
    chartHorasVentas.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;

    string HourLabel(int h) => h switch
    {
        0 => "00",
        6 => "06",
        12 => "12",
        18 => "18",
        23 => "23",
        _ => h % 2 == 0 ? h.ToString("00") : ""
    };

    chartHorasVentas.XAxes = new[]
    {
        new Axis
        {
            MinLimit = -0.5,
            MaxLimit = 23.5,
            Labels = Enumerable.Range(0,24).Select(HourLabel).ToArray(),
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(new SKColor(205,210,216)),
            SeparatorsPaint = null
        }
    };
    chartHorasVentas.YAxes = new[]
    {
        new Axis
        {
            Labels = __DAYS_ORDER,
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(new SKColor(205,210,216)),
            SeparatorsPaint = null
        }
    };

    // Guardar metadatos para el template de tooltip
    _heatmapMeta.Clear();
    foreach (var p in puntos)
    {
        int h = (int)p.X;
        int d = (int)p.Y;
        int ventas = (int)p.Weight;
        if (ventas > 0)
        {
            rank.TryGetValue((h, d), out var r);
            double pct = max == 0 ? 0 : ventas * 100.0 / max;
            _heatmapMeta[(h, d)] = (ventas, r, pct);
        }
    }
}
        #endregion

        private void CalcularDeltaVentas(List<Venta> ventasTodas, DateTime desde, DateTime hasta, decimal actual)
        {
            int rangoDias = (hasta.Date - desde.Date).Days + 1;
            var desdePrev = desde.AddDays(-rangoDias);
            var hastaPrev = desde.AddDays(-1).AddDays(1).AddTicks(-1);
            var prevVentas = ventasTodas.Where(v => v.Fecha >= desdePrev && v.Fecha <= hastaPrev).Sum(v => v.Total);
            if (prevVentas <= 0)
            {
                txtVentasDelta.Text = "";
                bdDeltaVentas.Visibility = Visibility.Collapsed;
                return;
            }
            var delta = (actual - prevVentas) / prevVentas * 100m;
            txtVentasDelta.Text = (delta >= 0 ? "▲ " : "▼ ") + Math.Abs(delta).ToString("N1") + "%";
            txtVentasDelta.Foreground = delta >= 0 ? (Brush)FindResource("BrushOk") : (Brush)FindResource("BrushErr");
            bdDeltaVentas.Background = delta >= 0
                ? new SolidColorBrush(Color.FromRgb(0x12, 0x3B, 0x27))
                : new SolidColorBrush(Color.FromRgb(0x4A, 0x1E, 0x1E));
            bdDeltaVentas.Visibility = Visibility.Visible;
        }

        private void CalcularMetaYProyeccion(List<Venta> ventasTodas) { /* reservado */ }

        private List<TopVentaDTO> ObtenerTopVendidos(DateTime desde, DateTime hasta, int limite, int? categoriaId, int? usuarioId)
        {
            var lista = new List<TopVentaDTO>();
            using var cn = new SQLiteConnection(_conn);
            cn.Open();
            var sql = @"
                SELECT p.Nombre, SUM(d.Cantidad) AS Cantidad
                FROM DetalleVentas d
                INNER JOIN Ventas v ON d.VentaId = v.Id
                INNER JOIN Productos p ON p.Id = d.ProductoId
                WHERE v.Estado='Activa' AND v.Fecha BETWEEN @Desde AND @Hasta
                {CAT}
                {USR}
                GROUP BY p.Nombre
                ORDER BY Cantidad DESC
                LIMIT @Limite;";
            sql = sql.Replace("{CAT}", categoriaId is > 0 ? "AND p.CategoriaId=@Cat" : "");
            sql = sql.Replace("{USR}", usuarioId is > 0 ? "AND v.UsuarioId=@Usr" : "");
            using var cmd = new SQLiteCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Desde", desde);
            cmd.Parameters.AddWithValue("@Hasta", hasta);
            cmd.Parameters.AddWithValue("@Limite", limite);
            if (categoriaId is > 0) cmd.Parameters.AddWithValue("@Cat", categoriaId.Value);
            if (usuarioId is > 0) cmd.Parameters.AddWithValue("@Usr", usuarioId.Value);
            using var rd = cmd.ExecuteReader();
            int pos = 1;
            while (rd.Read())
            {
                lista.Add(new TopVentaDTO
                {
                    Pos = pos++,
                    Nombre = rd.GetString(0),
                    Cantidad = rd.GetInt32(1)
                });
            }
            return lista;
        }

        #region Utilidades
        private (DateTime desde, DateTime hasta) ObtenerRangoFechas()
        {
            if (cbRangoFecha.SelectedItem is RangoFechaPreset preset && preset.Clave != "PERS")
                return preset.Rango();
            var dSel = (dpDesde.SelectedDate ?? DateTime.Today).Date;
            var hSel = (dpHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (hSel < dSel) hSel = dSel.AddDays(1).AddTicks(-1);
            return (dSel, hSel);
        }

        private int? ObtenerCategoriaSeleccionada() =>
            cbCategoria.SelectedValue is int id && id > 0 ? id : null;

        private int? ObtenerUsuarioSeleccionada() =>
            cbSucursal.SelectedValue is int id && id > 0 ? id : null;

        private HashSet<int> ObtenerVentaIdsPorCategoria(int categoriaId)
        {
            var ids = new HashSet<int>();
            using var cn = new SQLiteConnection(_conn);
            cn.Open();
            const string sql = @"
                SELECT DISTINCT v.Id
                FROM DetalleVentas d
                INNER JOIN Ventas v ON v.Id = d.VentaId
                INNER JOIN Productos p ON p.Id = d.ProductoId
                WHERE v.Estado='Activa' AND p.CategoriaId=@Cat;";
            using var cmd = new SQLiteCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Cat", categoriaId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) ids.Add(rd.GetInt32(0));
            return ids;
        }

        private HashSet<int> ObtenerCompraIdsPorCategoria(int categoriaId)
        {
            var ids = new HashSet<int>();
            using var cn = new SQLiteConnection(_conn);
            cn.Open();
            const string sql = @"
                SELECT DISTINCT c.Id
                FROM DetalleCompras d
                INNER JOIN Compras c ON c.Id = d.CompraId
                INNER JOIN Productos p ON p.Id = d.ProductoId
                WHERE c.Estado='Activa' AND p.CategoriaId=@Cat;";
            using var cmd = new SQLiteCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Cat", categoriaId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) ids.Add(rd.GetInt32(0));
            return ids;
        }

        private void AplicarColorStock()
        {
            if (decimal.TryParse(txtPorcStockBajo.Text.Replace("%", ""), out var val))
            {
                txtPorcStockBajo.Foreground =
                    val < 10 ? (Brush)FindResource("BrushOk") :
                    val < 25 ? (Brush)FindResource("BrushWarn") :
                               (Brush)FindResource("BrushErr");
            }
        }

        private void MostrarOverlay(bool v) => LoadingOverlay.Visibility = v ? Visibility.Visible : Visibility.Collapsed;

        private (DateTime desde, DateTime hasta)? ObtenerRango()
        {
            var (d, h) = ObtenerRangoFechas();
            return (d, h);
        }
        #endregion

        #region Exportar
        private SaveFileDialog CrearDialogo(string fileName, string titulo) => new()
        {
            Title = titulo,
            Filter = "CSV (*.csv)|*.csv",
            FileName = fileName,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool need = value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0;
            return need ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e) => ExportarVentasResumenCsv();
        private void BtnExportar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { ExportarVentasResumenCsv(); e.Handled = true; }
        }

        private void ExportarVentasResumenCsv()
        {
            try
            {
                var r = ObtenerRango(); if (r == null) return;
                var (desde, hasta) = r.Value;
                int? categoriaId = ObtenerCategoriaSeleccionada();
                int? usuarioId = ObtenerUsuarioSeleccionada();

                using var cn = new SQLiteConnection(_conn);
                cn.Open();
                var sql = @"
                    SELECT v.Id, v.Fecha, v.Total, IFNULL(u.Nombre, '') AS UsuarioNombre
                    FROM Ventas v
                    LEFT JOIN Usuarios u ON u.Id = v.UsuarioId
                    WHERE v.Estado='Activa' AND v.Fecha BETWEEN @D AND @H
                    {CAT_FILTER}
                    {USER_FILTER}
                    ORDER BY v.Fecha;";
                sql = sql.Replace("{CAT_FILTER}", categoriaId is > 0 ? @"AND EXISTS(
                                SELECT 1 FROM DetalleVentas dv
                                INNER JOIN Productos p2 ON p2.Id=dv.ProductoId
                                WHERE dv.VentaId=v.Id AND p2.CategoriaId=@Cat)" : "");
                sql = sql.Replace("{USER_FILTER}", usuarioId is > 0 ? "AND v.UsuarioId=@Usr" : "");

                using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", desde);
                cmd.Parameters.AddWithValue("@H", hasta);
                if (categoriaId is > 0) cmd.Parameters.AddWithValue("@Cat", categoriaId.Value);
                if (usuarioId is > 0) cmd.Parameters.AddWithValue("@Usr", usuarioId.Value);

                var ventas = new List<(int Id, DateTime Fecha, decimal Total, string Usuario)>();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        ventas.Add((rd.GetInt32(0), rd.GetDateTime(1),
                            Convert.ToDecimal(rd.GetValue(2)), rd.IsDBNull(3) ? "" : rd.GetString(3)));
                }

                if (!ventas.Any())
                {
                    MainSnackbar.MessageQueue?.Enqueue("Sin ventas en rango.");
                    return;
                }

                var dlg = CrearDialogo($"ventas_resumen_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", "Exportar ventas (Resumen)");
                if (dlg.ShowDialog(this) != true) return;
                var sb = new StringBuilder().AppendLine("Id;Fecha;Total;Usuario");
                foreach (var v in ventas)
                    sb.AppendLine($"{v.Id};{v.Fecha:yyyy-MM-dd HH:mm:ss};{v.Total:N2};{Csv(v.Usuario)}");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MainSnackbar.MessageQueue?.Enqueue("Resumen exportado");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando: " + ex.Message);
            }
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
                    "NuevaVenta" => () => { var v = new VentaWindow(_usuarioActualNombre, bloquearCodigo: true) { Owner = this }; v.Show(); }
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
        #endregion

        private void CardStockBajo_Click(object sender, MouseButtonEventArgs e)
        {
            if (dgStockBajo.Items.Count == 0)
                MainSnackbar.MessageQueue?.Enqueue("No hay productos con stock bajo.");
        }

        #region Tipos internos
        private record AccionDef(string Clave, string Titulo, string Icono);
        private class QuickAction
        {
            public string Clave { get; set; } = "";
            public string Titulo { get; set; } = "";
            public string Icono { get; set; } = "\uE10F";
            public Action? Ejecutar { get; set; }
        }
        private class TopVentaDTO
        {
            public int Pos { get; set; }
            public string Nombre { get; set; } = "";
            public int Cantidad { get; set; }
        }
        public class RangoFechaPreset
        {
            public string Clave { get; }
            public string Nombre { get; }
            public Func<(DateTime desde, DateTime hasta)> Rango { get; }
            public RangoFechaPreset(string clave, string nombre, Func<(DateTime, DateTime)> rango)
            { Clave = clave; Nombre = nombre; Rango = rango; }
        }
        public class OrdenRecienteDTO
        {
            public string Fecha { get; set; } = "";
            public string Cliente { get; set; } = "";
            public string Estado { get; set; } = "";
            public decimal Monto { get; set; }
            public string MontoStr => CurrencyService.FormatSoles(Monto, "N2");
        }
        #endregion

        private void TxtUmbralStock_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void CargarCategorias()
        {
            try
            {
                var cats = _categoriaService.ObtenerTodas()
                    .OrderBy(c => c.Nombre)
                    .Select(c => new { c.Id, c.Nombre })
                    .ToList();

                cbCategoria.ItemsSource =
                    new List<object> { new { Id = 0, Nombre = "Todas" } }
                    .Concat(cats)
                    .ToList();

                cbCategoria.SelectedIndex = 0;
            }
            catch
            {
                cbCategoria.ItemsSource = new List<object>
                {
                    new { Id = 0, Nombre = "Todas" }
                };
                cbCategoria.SelectedIndex = 0;
            }
        }
    }
}