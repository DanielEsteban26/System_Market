using System;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System_Market.Services;
using System_Market.Data;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.Collections.Generic;

namespace System_Market.Views
{
    public partial class ReportesWindow : Window
    {
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _productoService;
        private readonly CompraService _compraService;
        private readonly VentaService _ventaService;
        private readonly CategoriaService _categoria_service;

        public ReportesWindow()
        {
            InitializeComponent();
            var conn = DatabaseInitializer.GetConnectionString();
            _proveedorService = new ProveedorService(conn);
            _productoService = new ProductoService(conn);
            _compraService = new CompraService(conn);
            _ventaService = new VentaService(conn);
            _categoria_service = new CategoriaService(conn);

            try
            {
                cbRangoCompras.SelectedIndex = 0;
                cbRangoVentas.SelectedIndex = 0;
            }
            catch { }

            ApplyPresetToPickers(cbRangoCompras, dpDesdeCompras, dpHastaCompras);
            ApplyPresetToPickers(cbRangoVentas, dpDesdeVentas, dpHastaVentas);

            // Mostrar resumen inicial
            UpdateResumen();
        }

        private void CbRangoCompras_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ApplyPresetToPickers(cbRangoCompras, dpDesdeCompras, dpHastaCompras);

        private void CbRangoVentas_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ApplyPresetToPickers(cbRangoVentas, dpDesdeVentas, dpHastaVentas);

        private void FechaPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
            => UpdateResumen();

        private void ApplyPresetToPickers(ComboBox cb, DatePicker desde, DatePicker hasta)
        {
            if (cb == null || desde == null || hasta == null) return;
            if (cb.SelectedItem is ComboBoxItem it && it.Tag is string tag)
            {
                switch (tag)
                {
                    case "ULT7":
                        desde.SelectedDate = DateTime.Today.AddDays(-6);
                        hasta.SelectedDate = DateTime.Today;
                        desde.IsEnabled = hasta.IsEnabled = false;
                        break;
                    case "HOY":
                        desde.SelectedDate = DateTime.Today;
                        hasta.SelectedDate = DateTime.Today;
                        desde.IsEnabled = hasta.IsEnabled = false;
                        break;
                    case "MES":
                        desde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        hasta.SelectedDate = DateTime.Today;
                        desde.IsEnabled = hasta.IsEnabled = false;
                        break;
                    case "PERS":
                        desde.IsEnabled = hasta.IsEnabled = true;
                        if (desde.SelectedDate == null) desde.SelectedDate = DateTime.Today.AddDays(-7);
                        if (hasta.SelectedDate == null) hasta.SelectedDate = DateTime.Today;
                        break;
                }
            }

            // refrescar resumen al aplicar preset
            UpdateResumen();
        }

        // Actualiza los valores del resumen (Ventas, Compras, Balance)
        private void UpdateResumen()
        {
            try
            {
                DateTime desdeC = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                DateTime hastaC = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

                DateTime desdeV = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                DateTime hastaV = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

                // Totales simples (como antes)
                decimal totalCompras = _compraService.ObtenerTodas()
                    .Where(c => c.Fecha >= desdeC && c.Fecha <= hastaC)
                    .Sum(c => c.Total);

                decimal totalVentas = _ventaService.ObtenerTodas()
                    .Where(v => v.Fecha >= desdeV && v.Fecha <= hastaV)
                    .Sum(v => v.Total);

                decimal balance = totalVentas - totalCompras;

                // --- CÁLCULO DE COGS (coste de lo vendido) ---
                // 1) Cargar productos en memoria para lookup rápido
                var productos = _productoService.ObtenerTodos().ToDictionary(p => p.Id, p => p);

                // 2) Obtener ventas activas en rango
                var ventas = _ventaService.ObtenerTodas()
                    .Where(v => v.Fecha >= desdeV && v.Fecha <= hastaV && string.Equals(v.Estado, "Activa", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal cogs = 0m;
                foreach (var v in ventas)
                {
                    // Obtener detalles por venta (método disponible en VentaService)
                    var detalles = _ventaService.ObtenerDetallesPorVenta(v.Id);
                    if (detalles == null) continue;
                    foreach (var d in detalles)
                    {
                        // Preferir precio de compra del producto (histórico no disponible en DetalleVenta)
                        decimal precioCompra = 0m;
                        if (productos.TryGetValue(d.ProductoId, out var prod) && prod != null)
                            precioCompra = prod.PrecioCompra;

                        cogs += precioCompra * d.Cantidad;
                    }
                }

                decimal gananciaBruta = totalVentas - cogs;

                // Asignar a UI (manteniendo los TextBlocks que ya tienes)
                txtVentasValor.Text = $"S/ {totalVentas:N2}";
                txtComprasValor.Text = $"S/ {totalCompras:N2}";
                txtBalanceValor.Text = $"S/ {balance:N2}";

                // Colorear balance
                txtBalanceValor.Foreground = balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(0x8A, 0xF6, 0xC6))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x65));

                // Mostrar Ganancia bruta en tooltip (sin tocar XAML)
                txtBalanceValor.ToolTip = $"Ganancia bruta (Ventas − COGS): S/ {gananciaBruta:N2}\nCOGS calculado = S/ {cogs:N2}\n(Usa PrecioCompra actual de Productos).";
            }
            catch
            {
                txtVentasValor.Text = "S/ 0.00";
                txtComprasValor.Text = "S/ 0.00";
                txtBalanceValor.Text = "S/ 0.00";
                txtBalanceValor.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                txtBalanceValor.ToolTip = null;
            }
        }

        private bool AskSaveFile(string title, string defaultName, string filter, out string path)
        {
            path = null!;
            var dlg = new SaveFileDialog
            {
                Title = title,
                FileName = defaultName,
                Filter = filter,
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
            return true;
        }

        private void ExportToXlsx(string suggestedName, string[] headers, Func<IXLRow, int, bool> filler, string sheetName = "Hoja1")
        {
            if (!AskSaveFile("Guardar Excel", suggestedName + ".xlsx", "Excel (*.xlsx)|*.xlsx", out var path)) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(sheetName);

                for (int c = 0; c < headers.Length; c++)
                    ws.Cell(1, c + 1).Value = headers[c];

                int row = 2;
                while (true)
                {
                    var ixlRow = ws.Row(row);
                    bool hasMore = filler(ixlRow, row);
                    if (!hasMore) break;
                    row++;
                }

                ws.ColumnsUsed().AdjustToContents();
                wb.SaveAs(path);
                MessageBox.Show("Exportado correctamente:\n" + path, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando a Excel: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveCsv(string suggestedName, string[] headers, Func<StringBuilder, int, bool> filler)
        {
            if (!AskSaveFile("Guardar CSV", suggestedName + ".csv", "CSV (*.csv)|*.csv", out var path)) return false;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(";", headers));
                int index = 0;
                while (true)
                {
                    var rowBuilder = new StringBuilder();
                    bool hasMore = filler(rowBuilder, index);
                    if (!hasMore) break;
                    sb.AppendLine(rowBuilder.ToString());
                    index++;
                }
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exportado correctamente:\n" + path, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error guardando CSV: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ExportPdfFromLines(string suggestedName, string title, string[] lines)
        {
            if (!AskSaveFile("Guardar PDF", suggestedName + ".pdf", "PDF (*.pdf)|*.pdf", out var path)) return;

            try
            {
                using var doc = new PdfDocument();
                var page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Verdana", 14, XFontStyle.Bold);
                var font = new XFont("Verdana", 10, XFontStyle.Regular);
                double y = 40;
                gfx.DrawString(title, fontTitle, XBrushes.Black, new XRect(40, y, page.Width - 80, 30), XStringFormats.TopLeft);
                y += 30;
                double lineHeight = font.GetHeight() + 4;
                foreach (var ln in lines)
                {
                    if (y + lineHeight > page.Height - 40)
                    {
                        page = doc.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 40;
                    }
                    gfx.DrawString(ln, font, XBrushes.Black, new XRect(40, y, page.Width - 80, lineHeight), XStringFormats.TopLeft);
                    y += lineHeight;
                }

                using var fs = File.Create(path);
                doc.Save(fs);
                MessageBox.Show("PDF creado correctamente:\n" + path, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando a PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveBitmapFromVisual(DrawingVisual visual, string suggestedName)
        {
            if (visual == null) return false;
            if (!AskSaveFile("Guardar imagen", suggestedName + ".png", "PNG (*.png)|*.png", out var path)) return false;

            var bounds = visual.ContentBounds;
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width)) + 10;
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height)) + 10;

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.OpenWrite(path);
            encoder.Save(fs);
            MessageBox.Show("Imagen guardada:\n" + path, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private void ShowPreviewAndSave(DrawingVisual visual, string suggestedName)
        {
            if (visual == null) return;

            var bounds = visual.ContentBounds;
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width)) + 10;
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height)) + 10;

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var img = new System.Windows.Controls.Image { Source = rtb, Stretch = Stretch.None, Margin = new Thickness(8) };

            var saveBtn = new Button { Content = "Guardar PNG", Width = 110, Height = 32, Margin = new Thickness(8) };
            var closeBtn = new Button { Content = "Cerrar", Width = 80, Height = 32, Margin = new Thickness(8) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btnPanel.Children.Add(saveBtn);
            btnPanel.Children.Add(closeBtn);

            var panel = new DockPanel();
            DockPanel.SetDock(btnPanel, Dock.Bottom);
            panel.Children.Add(btnPanel);
            panel.Children.Add(new ScrollViewer { Content = img, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            var win = new Window
            {
                Title = suggestedName,
                Content = panel,
                Width = Math.Min(1000, width + 60),
                Height = Math.Min(800, height + 120),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            saveBtn.Click += (_, __) =>
            {
                if (AskSaveFile("Guardar imagen", suggestedName + ".png", "PNG (*.png)|*.png", out var path))
                {
                    try
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rtb));
                        using var fs = File.OpenWrite(path);
                        encoder.Save(fs);
                        MessageBox.Show("Imagen guardada:\n" + path, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                        win.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error guardando imagen: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            closeBtn.Click += (_, __) => win.Close();

            win.ShowDialog();
        }

        private DrawingVisual BuildSimpleBarChart(string title, (string label, double value)[] data)
        {
            const double width = 1000, height = 500;
            var dv = new DrawingVisual();
            using var dc = dv.RenderOpen();
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var ft = new Typeface("Segoe UI");
            var formattedTitle = new FormattedText(title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ft, 18, Brushes.Black, 1.0);
            dc.DrawText(formattedTitle, new Point(20, 8));

            double max = data.Length == 0 ? 1 : Math.Max(1, data.Max(d => d.value));
            double leftMargin = 80;
            double rightMargin = 40;
            double barAreaWidth = width - leftMargin - rightMargin;
            double barW = barAreaWidth / Math.Max(1, data.Length);
            double x = leftMargin;
            double yBase = height - 60;
            var axisPen = new Pen(Brushes.Black, 1);
            dc.DrawLine(axisPen, new Point(leftMargin - 10, yBase), new Point(width - rightMargin, yBase));

            for (int i = 0; i < data.Length; i++)
            {
                double h = (data[i].value / max) * (height - 160);
                var rect = new Rect(x + i * barW + barW * 0.15, yBase - h, barW * 0.7, h);
                dc.DrawRectangle(Brushes.SteelBlue, null, rect);

                var lbl = new FormattedText(data[i].label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ft, 12, Brushes.Black, 1.0)
                { MaxTextWidth = barW };
                dc.PushTransform(new TranslateTransform(x + i * barW + barW * 0.15, yBase + 6));
                dc.DrawText(lbl, new Point(0, 0));
                dc.Pop();

                // Formateo con separadores de miles: si es entero mostramos "N0", si tiene decimales "N2"
                string valStr;
                if (Math.Abs(data[i].value % 1) < 0.0000001)
                    valStr = data[i].value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
                else
                    valStr = data[i].value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

                var valText = new FormattedText(valStr,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ft, 12, Brushes.Black, 1.0);
                double vtX = rect.X + (rect.Width - valText.Width) / 2;
                double vtY = rect.Y - valText.Height - 4;
                dc.DrawText(valText, new Point(Math.Max(vtX, rect.X), Math.Max(vtY, 10)));
            }

            dv.Offset = new Vector(0, 0);
            return dv;
        }

        // Productos
        private void BtnExportarProductosExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool incluir = chkIncluirAgotados?.IsChecked == true;
                var list = _productoService.ObtenerTodos()
                    .Where(p => incluir || p.Stock > 0)
                    .OrderBy(p => p.Nombre)
                    .ToList();

                if (!list.Any()) { MessageBox.Show("No hay productos para exportar.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                ExportToXlsx("productos", new[] { "Id", "Codigo", "Nombre", "Categoria", "Proveedor", "PrecioCompra", "PrecioVenta", "Stock" },
                    (row, indexRow) =>
                    {
                        int idx = indexRow - 2;
                        if (idx >= list.Count) return false;
                        var p = list[idx];
                        row.Cell(1).Value = p.Id;
                        row.Cell(2).Value = p.CodigoBarras ?? "";
                        row.Cell(3).Value = p.Nombre ?? "";
                        row.Cell(4).Value = p.CategoriaNombre ?? "";
                        row.Cell(5).Value = p.ProveedorNombre ?? "";
                        row.Cell(6).Value = p.PrecioCompra;
                        row.Cell(7).Value = p.PrecioVenta;
                        row.Cell(8).Value = p.Stock;
                        return true;
                    });
            }
            catch (Exception ex) { MessageBox.Show("Error exportando: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarProductosPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool incluir = chkIncluirAgotados?.IsChecked == true;
                var list = _productoService.ObtenerTodos()
                    .Where(p => incluir || p.Stock > 0)
                    .OrderBy(p => p.Nombre)
                    .ToList();

                if (!list.Any()) { MessageBox.Show("No hay productos para exportar.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var lines = list.Select(p =>
                    $"{p.Id}; {p.CodigoBarras ?? ""}; {p.Nombre}; {p.CategoriaNombre ?? ""}; {p.ProveedorNombre ?? ""}; {p.PrecioCompra:N2}; {p.PrecioVenta:N2}; {p.Stock}"
                ).ToArray();

                ExportPdfFromLines("productos", "Productos", lines);
            }
            catch (Exception ex) { MessageBox.Show("Error exportando productos a PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarProductosGrafico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var top = _productoService.ObtenerTodos().OrderByDescending(p => p.Stock).Take(8)
                          .Select(p => (label: p.Nombre ?? p.CodigoBarras ?? "Producto", value: (double)p.Stock)).ToArray();
                var dv = BuildSimpleBarChart("Stock - productos (top 8)", top);
                ShowPreviewAndSave(dv, "productos_stock");
            }
            catch (Exception ex) { MessageBox.Show("Error generando gráfico: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Guarda el DrawingVisual como PNG en %TEMP% y devuelve la ruta
        private string? SaveBitmapToTemp(DrawingVisual visual, string suggestedName)
        {
            if (visual == null) return null;
            var bounds = visual.ContentBounds;
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width)) + 10;
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height)) + 10;

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            try
            {
                var fileName = $"{suggestedName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(Path.GetTempPath(), fileName);
                using var fs = File.OpenWrite(path);
                encoder.Save(fs);
                return path;
            }
            catch
            {
                return null;
            }
        }

        // Productos - rápido: guarda directamente en %TEMP% sin vista previa ni diálogo
        private void BtnExportarProductosGraficoRapido_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var top = _productoService.ObtenerTodos()
                    .OrderByDescending(p => p.Stock)
                    .Take(8)
                    .Select(p => (label: p.Nombre ?? p.CodigoBarras ?? "Producto", value: (double)p.Stock))
                    .ToArray();

                if (!top.Any())
                {
                    MessageBox.Show("No hay datos para generar el gráfico rápido de productos.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dv = BuildSimpleBarChart("Stock - productos (top 8)", top);
                var path = SaveBitmapToTemp(dv, "productos_stock_quick");
                if (path != null) MessageBox.Show("Gráfico rápido guardado en:\n" + path, "Exportar rápido", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show("No se pudo guardar el gráfico rápido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generando gráfico rápido de productos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Compras - rápido: guarda directamente en %TEMP% sin vista previa ni diálogo
        private void BtnExportarComprasGraficoRapido_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var compras = _compraService.ObtenerTodas().Where(c => c.Fecha >= desde && c.Fecha <= hasta).ToList();
                if (!compras.Any()) { MessageBox.Show("No hay compras en el rango para gráfico rápido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var grouped = compras.GroupBy(c => c.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => (label: g.Key.ToString("yyyy-MM-dd"), value: (double)g.Sum(x => (double)x.Total)))
                    .ToArray();

                var dv = BuildSimpleBarChart($"Compras (Total) {desde:yyyy-MM-dd} - {hasta:yyyy-MM-dd}", grouped);
                var path = SaveBitmapToTemp(dv, "compras_grafico_quick");
                if (path != null) MessageBox.Show("Gráfico rápido guardado en:\n" + path, "Exportar rápido", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show("No se pudo guardar el gráfico rápido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generando gráfico rápido de compras: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Ventas - rápido: guarda directamente en %TEMP% sin vista previa ni diálogo
        private void BtnExportarVentasGraficoRapido_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var ventas = _ventaService.ObtenerTodas().Where(v => v.Fecha >= desde && v.Fecha <= hasta && v.Estado == "Activa").ToList();
                if (!ventas.Any()) { MessageBox.Show("No hay ventas activas en el rango para gráfico rápido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var grouped = ventas.GroupBy(v => v.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => (label: g.Key.ToString("yyyy-MM-dd"), value: (double)g.Sum(x => (double)x.Total)))
                    .ToArray();

                var dv = BuildSimpleBarChart($"Ventas (Total) {desde:yyyy-MM-dd} - {hasta:yyyy-MM-dd}", grouped);
                var path = SaveBitmapToTemp(dv, "ventas_grafico_quick");
                if (path != null) MessageBox.Show("Gráfico rápido guardado en:\n" + path, "Exportar rápido", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show("No se pudo guardar el gráfico rápido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generando gráfico rápido de ventas: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Proveedores
        private void BtnExportarProveedoresExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var list = _proveedorService.ObtenerTodos().OrderBy(p => p.Nombre).ToList();
                if (!list.Any()) { MessageBox.Show("Sin proveedores.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                ExportToXlsx("proveedores", new[] { "Id", "Nombre", "RUC", "Telefono" },
                    (row, indexRow) =>
                    {
                        int idx = indexRow - 2;
                        if (idx >= list.Count) return false;
                        var pr = list[idx];
                        row.Cell(1).Value = pr.Id;
                        row.Cell(2).Value = pr.Nombre ?? "";
                        row.Cell(3).Value = pr.RUC ?? "";
                        row.Cell(4).Value = pr.Telefono ?? "";
                        return true;
                    });
            }
            catch (Exception ex) { MessageBox.Show("Error exportando proveedores: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarProveedoresGrafico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productos = _productoService.ObtenerTodos();
                var groups = productos.GroupBy(p => string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "Sin proveedor" : p.ProveedorNombre)
                    .Select(g => (label: g.Key, value: (double)g.Count()))
                    .OrderByDescending(x => x.value)
                    .Take(12)
                    .ToArray();

                if (!groups.Any()) { MessageBox.Show("No hay datos para generar el gráfico de proveedores.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var dv = BuildSimpleBarChart("Productos por Proveedor (top 12)", groups);
                ShowPreviewAndSave(dv, "proveedores_grafico");
            }
            catch (Exception ex) { MessageBox.Show("Error generando gráfico de proveedores: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Compras
        private void BtnExportarComprasExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var list = _compraService.ObtenerTodas().Where(c => c.Fecha >= desde && c.Fecha <= hasta).OrderBy(c => c.Fecha).ToList();
                if (!list.Any()) { MessageBox.Show("No hay compras en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                ExportToXlsx($"compras_{desde:yyyyMMdd}_{hasta:yyyyMMdd}",
                    new[] { "Id", "Fecha", "Proveedor", "Total", "Estado", "MotivoAnulacion" },
                    (row, indexRow) =>
                    {
                        int idx = indexRow - 2;
                        if (idx >= list.Count) return false;
                        var c = list[idx];
                        row.Cell(1).Value = c.Id;
                        row.Cell(2).Value = c.Fecha;
                        row.Cell(3).Value = c.ProveedorNombre ?? "";
                        row.Cell(4).Value = c.Total;
                        row.Cell(5).Value = c.Estado ?? "";
                        row.Cell(6).Value = c.MotivoAnulacion ?? "";
                        return true;
                    });

            }
            catch (Exception ex) { MessageBox.Show("Error exportando compras: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarComprasPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                var list = _compraService.ObtenerTodas().Where(c => c.Fecha >= desde && c.Fecha <= hasta).OrderBy(c => c.Fecha).ToList();
                if (!list.Any()) { MessageBox.Show("No hay compras en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var lines = list.Select(c => $"{c.Id}; {c.Fecha:yyyy-MM-dd HH:mm}; {c.ProveedorNombre}; {c.Total:N2}; {c.Estado}").ToArray();
                ExportPdfFromLines($"compras_{desde:yyyyMMdd}_{hasta:yyyyMMdd}", "Compras", lines);
            }
            catch (Exception ex) { MessageBox.Show("Error exportando a PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarComprasGrafico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var compras = _compraService.ObtenerTodas().Where(c => c.Fecha >= desde && c.Fecha <= hasta).ToList();
                if (!compras.Any()) { MessageBox.Show("No hay compras en el rango para gráfico.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var grouped = compras.GroupBy(c => c.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => (label: g.Key.ToString("yyyy-MM-dd"), value: (double)g.Sum(x => (double)x.Total)))
                    .ToArray();

                var dv = BuildSimpleBarChart($"Compras (Total) {desde:yyyy-MM-dd} - {hasta:yyyy-MM-dd}", grouped);
                ShowPreviewAndSave(dv, "compras_grafico");
            }
            catch (Exception ex) { MessageBox.Show("Error generando gráfico de compras: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Ventas
        private void BtnExportarVentasExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var list = _ventaService.ObtenerTodas().Where(v => v.Fecha >= desde && v.Fecha <= hasta).OrderBy(v => v.Fecha).ToList();
                if (!list.Any()) { MessageBox.Show("No hay ventas en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                ExportToXlsx($"ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}",
                    new[] { "Id", "Fecha", "Usuario", "Total", "Estado", "MotivoAnulacion" },
                    (row, indexRow) =>
                    {
                        int idx = indexRow - 2;
                        if (idx >= list.Count) return false;
                        var v = list[idx];
                        row.Cell(1).Value = v.Id;
                        row.Cell(2).Value = v.Fecha;
                        row.Cell(3).Value = v.UsuarioNombre ?? "";
                        row.Cell(4).Value = v.Total;
                        row.Cell(5).Value = v.Estado ?? "";
                        row.Cell(6).Value = v.MotivoAnulacion ?? "";
                        return true;
                    });
            }
            catch (Exception ex) { MessageBox.Show("Error exportando ventas: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarVentasPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                var list = _ventaService.ObtenerTodas().Where(v => v.Fecha >= desde && v.Fecha <= hasta).OrderBy(v => v.Fecha).ToList();
                if (!list.Any()) { MessageBox.Show("No hay ventas en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var lines = list.Select(v => $"{v.Id}; {v.Fecha:yyyy-MM-dd HH:mm}; {v.UsuarioNombre}; {v.Total:N2}; {v.Estado}").ToArray();
                ExportPdfFromLines($"ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}", "Ventas", lines);
            }
            catch (Exception ex) { MessageBox.Show("Error exportando a PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarVentasGrafico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) { MessageBox.Show("Rango inválido.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var ventas = _ventaService.ObtenerTodas().Where(v => v.Fecha >= desde && v.Fecha <= hasta && v.Estado == "Activa").ToList();
                if (!ventas.Any()) { MessageBox.Show("No hay ventas activas en el rango para gráfico.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var grouped = ventas.GroupBy(v => v.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => (label: g.Key.ToString("yyyy-MM-dd"), value: (double)g.Sum(x => (double)x.Total)))
                    .ToArray();

                var dv = BuildSimpleBarChart($"Ventas (Total) {desde:yyyy-MM-dd} - {hasta:yyyy-MM-dd}", grouped);
                ShowPreviewAndSave(dv, "ventas_grafico");
            }
            catch (Exception ex) { MessageBox.Show("Error generando gráfico de ventas: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Categorías
        private void BtnExportarCategoriasGrafico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productos = _productoService.ObtenerTodos();
                var groups = productos.GroupBy(p => string.IsNullOrWhiteSpace(p.CategoriaNombre) ? "Sin categoría" : p.CategoriaNombre)
                    .Select(g => (label: g.Key, value: (double)g.Count()))
                    .OrderByDescending(x => x.value)
                    .Take(12)
                    .ToArray();

                if (!groups.Any()) { MessageBox.Show("No hay datos por categoría para el gráfico.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var dv = BuildSimpleBarChart("Productos por Categoría (top 12)", groups);
                ShowPreviewAndSave(dv, "categorias_grafico");
            }
            catch (Exception ex) { MessageBox.Show("Error generando gráfico: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportarCategoriasExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var list = _categoria_service.ObtenerTodas().OrderBy(c => c.Nombre).ToList();
                if (!list.Any()) { MessageBox.Show("Sin categorías.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                ExportToXlsx("categorias", new[] { "Id", "Nombre" },
                    (row, indexRow) =>
                    {
                        int idx = indexRow - 2;
                        if (idx >= list.Count) return false;
                        var c = list[idx];
                        row.Cell(1).Value = c.Id;
                        row.Cell(2).Value = c.Nombre ?? "";
                        return true;
                    });
            }
            catch (Exception ex) { MessageBox.Show("Error exportando categorías: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }



        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool need = value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0;
            return need ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private void BtnHistorialVentas_Click(object sender, RoutedEventArgs e)
        {
            var w = new HistorialVentasWindow { Owner = this };
            w.ShowDialog();

        }

        private void BtnHistorialCompras_Click(object sender, RoutedEventArgs e)
        {
            var w = new HistorialComprasWindow { Owner = this };
            w.ShowDialog();
        }
    }
}