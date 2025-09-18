using System;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System_Market.Services;
using System_Market.Data;
using System.IO;

namespace System_Market.Views
{
    public partial class ReportesWindow : Window
    {
        private readonly ProveedorService _proveedorService;
        private readonly ProductoService _productoService;
        private readonly CompraService _compraService;
        private readonly VentaService _ventaService;
        private readonly CategoriaService _categoriaService;

        public ReportesWindow()
        {
            InitializeComponent();

            // Inicializar services
            var conn = DatabaseInitializer.GetConnectionString();
            _proveedorService = new ProveedorService(conn);
            _productoService = new ProductoService(conn);
            _compraService = new CompraService(conn);
            _ventaService = new VentaService(conn);
            _categoriaService = new CategoriaService(conn);

            LoadProveedores();
            // Fecha por defecto: últimos 7 días
            dpDesdeCompras.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaCompras.SelectedDate = DateTime.Today;
            dpDesdeVentas.SelectedDate = DateTime.Today.AddDays(-7);
            dpHastaVentas.SelectedDate = DateTime.Today;
        }

        private void LoadProveedores()
        {
            try
            {
                var proveedores = _proveedorService.ObtenerTodos().OrderBy(p => p.Nombre).ToList();
                cbProveedores.ItemsSource = proveedores;
                cbProveedores.DisplayMemberPath = "Nombre";
                cbProveedores.SelectedValuePath = "Id";
                if (proveedores.Any()) cbProveedores.SelectedIndex = 0;
            }
            catch
            {
                // ignore load errors
            }
        }

        // --- Helpers CSV / Save ---
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool need = value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0;
            return need ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private bool SaveTextFile(string suggestedFileName, string content, string filter = "CSV (*.csv)|*.csv|Todos los archivos|*.*")
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Guardar como",
                    Filter = filter,
                    FileName = suggestedFileName,
                    AddExtension = true,
                    OverwritePrompt = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dlg.ShowDialog(this) != true) return false;
                File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
                MessageBox.Show("Exportado correctamente:\n" + dlg.FileName, "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar archivo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // --- Exportaciones implementadas ---
        private void BtnExportarProductosExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var incluirAgotados = chkIncluirAgotados?.IsChecked == true;
                var productos = _productoService.ObtenerTodos()
                    .Where(p => incluirAgotados || p.Stock > 0)
                    .OrderBy(p => p.Nombre)
                    .ToList();

                if (productos.Count == 0)
                {
                    MessageBox.Show("No hay productos para exportar.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Id;Código;Nombre;Categoría;Proveedor;PrecioCompra;PrecioVenta;Stock");
                foreach (var p in productos)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        p.Id.ToString(),
                        CsvEscape(p.CodigoBarras ?? ""),
                        CsvEscape(p.Nombre ?? ""),
                        CsvEscape(p.CategoriaNombre ?? ""),
                        CsvEscape(p.ProveedorNombre ?? ""),
                        p.PrecioCompra.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                        p.PrecioVenta.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                        p.Stock.ToString()
                    }));
                }

                SaveTextFile($"productos_{DateTime.Today:yyyyMMdd}.csv", sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando productos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportarProveedoresExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var proveedores = _proveedorService.ObtenerTodos().OrderBy(p => p.Nombre).ToList();
                if (proveedores.Count == 0) { MessageBox.Show("No hay proveedores.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var sb = new StringBuilder();
                sb.AppendLine("Id;Nombre;RUC;Telefono");
                foreach (var pr in proveedores)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        pr.Id.ToString(),
                        CsvEscape(pr.Nombre ?? ""),
                        CsvEscape(pr.RUC ?? ""),
                        CsvEscape(pr.Telefono ?? "")
                    }));
                }

                SaveTextFile($"proveedores_{DateTime.Today:yyyyMMdd}.csv", sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando proveedores: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportarComprasExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeCompras.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaCompras.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

                var compras = _compraService.ObtenerTodas()
                    .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
                    .OrderBy(c => c.Fecha)
                    .ToList();

                if (!compras.Any()) { MessageBox.Show("No hay compras en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var sb = new StringBuilder();
                sb.AppendLine("Id;Fecha;Proveedor;Total;Estado;MotivoAnulacion");
                foreach (var c in compras)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        c.Id.ToString(),
                        CsvEscape(c.Fecha.ToString("yyyy-MM-dd HH:mm:ss")),
                        CsvEscape(c.ProveedorNombre ?? ""),
                        c.Total.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                        CsvEscape(c.Estado ?? ""),
                        CsvEscape(c.MotivoAnulacion ?? "")
                    }));
                }

                SaveTextFile($"compras_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando compras: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportarVentasExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desde = (dpDesdeVentas.SelectedDate ?? DateTime.Today).Date;
                var hasta = (dpHastaVentas.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                if (hasta < desde) hasta = desde.AddDays(1).AddTicks(-1);

                var ventas = _ventaService.ObtenerTodas()
                    .Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                    .OrderBy(v => v.Fecha)
                    .ToList();

                if (!ventas.Any()) { MessageBox.Show("No hay ventas en el rango.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var sb = new StringBuilder();
                sb.AppendLine("Id;Fecha;Usuario;Total;Estado;MotivoAnulacion");
                foreach (var v in ventas)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        v.Id.ToString(),
                        CsvEscape(v.Fecha.ToString("yyyy-MM-dd HH:mm:ss")),
                        CsvEscape(v.UsuarioNombre ?? ""),
                        v.Total.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                        CsvEscape(v.Estado ?? ""),
                        CsvEscape(v.MotivoAnulacion ?? "")
                    }));
                }

                SaveTextFile($"ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv", sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exportando ventas: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // PDF / gráficos: stubs (puedo implementar con librería si quieres)
        private void BtnExportarComprasPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportar a PDF no implementado. ¿Deseas que lo implemente (iTextSharp o PdfSharp)?", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportarVentasPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportar a PDF no implementado. ¿Deseas que lo implemente (iTextSharp o PdfSharp)?", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportarCategoriasGrafico_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportar gráfico no implementado aún. Puedo añadir LiveCharts si quieres.", "Gráfico", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportarProductosGrafico_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportar gráfico no implementado aún. Puedo añadir LiveCharts si quieres.", "Gráfico", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnHistorial_Click(object sender, RoutedEventArgs e)
        {
            var w = new HistorialComprasWindow { Owner = this };
            w.ShowDialog();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}