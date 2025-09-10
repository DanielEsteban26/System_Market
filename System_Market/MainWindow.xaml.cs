using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System_Market.Data;
using System_Market.Services;
using System_Market.Views;

namespace System_Market
{
    public partial class MainWindow : Window
    {
        private readonly string _conn;
        private readonly ProductoService _productoService;
        private readonly VentaService _ventaService;
        private readonly CompraService _compraService;
        private const int UMBRAL_STOCK_BAJO = 5;

        // Acciones rápidas dinámicas
        private readonly ObservableCollection<QuickAction> _accionesRapidas = new();
        private readonly List<AccionDef> _catalogoAcciones = new()
        {
            new AccionDef("NuevaVenta", "Nueva Venta", "\uE73E"),
            new AccionDef("NuevaCompra", "Nueva Compra", "\uE73E"),
            new AccionDef("AgregarProducto", "Agregar Producto", "\uE710"),
            new AccionDef("NuevoProveedor", "Proveedor Nuevo", "\uE716"),
            new AccionDef("Productos", "Listado Productos", "\uE14C"),
            new AccionDef("Ventas", "Listado Ventas", "\uE7BF"),
            new AccionDef("Compras", "Listado Compras", "\uE73E"),
            new AccionDef("Proveedores", "Listado Proveedores", "\uE716"),
            new AccionDef("Usuarios", "Gestión Usuarios", "\uE77B"),
            new AccionDef("Categorias", "Gestión Categorías", "\uE1CB"),
            new AccionDef("Dashboard", "Dashboard", "\uE9D5"),
        };

        // Persistencia
        private string AccionesFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "System_Market", "quick_actions.json");

        public MainWindow()
        {
            InitializeComponent();

            BarcodeScannerService.Start(); // inicia hook global del lector

            _conn = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(_conn);
            _ventaService = new VentaService(_conn);
            _compraService = new CompraService(_conn);

            // Bind dinámico
            icQuickActions.ItemsSource = _accionesRapidas;
            cbAccion.ItemsSource = _catalogoAcciones;

            // Cargar acciones guardadas, o semillas si no hay
            if (!CargarAccionesGuardadas())
            {
                AgregarAccionSiNoExiste("NuevaVenta");
                AgregarAccionSiNoExiste("NuevaCompra");
                AgregarAccionSiNoExiste("AgregarProducto");
                AgregarAccionSiNoExiste("NuevoProveedor");
                GuardarAcciones();
            }

            // Guardar al cerrar
            this.Closed += (_, __) => GuardarAcciones();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtUsuarioActual.Text = "Usuario Demo";
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                var start = DateTime.Today;
                var end = start.AddDays(1).AddTicks(-1);

                var result = await Task.Run(() =>
                {
                    var productos = _productoService.ObtenerTodos();
                    var ventas = _ventaService.ObtenerTodas()
                        .Where(v => v.Estado == "Activa" && v.Fecha >= start && v.Fecha <= end)
                        .ToList();
                    var compras = _compraService.ObtenerTodas()
                        .Where(c => c.Estado == "Activa" && c.Fecha >= start && c.Fecha <= end)
                        .ToList();
                    var masVendido = ObtenerProductoMasVendidoEnRango(start, end);
                    return (productos, ventas, compras, masVendido);
                });

                txtTotalProductos.Text = result.productos.Count.ToString();
                txtStockBajo.Text = result.productos.Count(p => p.Stock <= UMBRAL_STOCK_BAJO).ToString();
                txtVentasHoy.Text = "S/ " + result.ventas.Sum(v => v.Total).ToString("0.00");
                txtComprasHoy.Text = "S/ " + result.compras.Sum(c => c.Total).ToString("0.00");
                txtProductoMasVendido.Text = result.masVendido ?? "Sin ventas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al refrescar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? ObtenerProductoMasVendidoEnRango(DateTime desde, DateTime hasta)
        {
            try
            {
                using var cn = new SQLiteConnection(_conn);
                cn.Open();
                string sql = @"
                    SELECT p.Nombre, SUM(d.Cantidad) AS Total
                    FROM DetalleVentas d
                    INNER JOIN Ventas v ON d.VentaId = v.Id
                    INNER JOIN Productos p ON d.ProductoId = p.Id
                    WHERE v.Estado='Activa'
                      AND v.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY p.Nombre
                    ORDER BY Total DESC
                    LIMIT 1;";
                using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Desde", desde);
                cmd.Parameters.AddWithValue("@Hasta", hasta);
                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    string nombre = rd.GetString(0);
                    long total = rd.GetInt64(1);
                    return $"{nombre} ({total})";
                }
            }
            catch
            {
                // Ignorar y devolver null
            }
            return null;
        }

        private async void BtnRefrescarDashboard_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void BtnNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            new VentaWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnNuevaCompra_Click(object sender, RoutedEventArgs e)
        {
            new CompraWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoEdicionWindow(_conn);
            if (win.ShowDialog() == true)
            {
                _productoService.AgregarProducto(win.Producto);
            }
            await RefreshAsync();
        }
        private async void BtnNuevoProveedor_Click(object sender, RoutedEventArgs e)
        {
            new ProveedorWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnProductos_Click(object sender, RoutedEventArgs e)
        {
            new ProductoWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnCategorias_Click(object sender, RoutedEventArgs e)
        {
            new CategoriaWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnProveedores_Click(object sender, RoutedEventArgs e)
        {
            new ProveedorWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnUsuarios_Click(object sender, RoutedEventArgs e)
        {
            new UsuarioWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnVentas_Click(object sender, RoutedEventArgs e)
        {
            new VentaWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnCompras_Click(object sender, RoutedEventArgs e)
        {
            new CompraWindow().ShowDialog();
            await RefreshAsync();
        }
        private async void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow().ShowDialog();
            await RefreshAsync();
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
        }

        // ====== Acciones rápidas dinámicas (con persistencia) ======
        private void BtnAgregarAccion_Click(object sender, RoutedEventArgs e)
        {
            if (cbAccion.SelectedValue is string clave)
            {
                if (AgregarAccionSiNoExiste(clave))
                    GuardarAcciones();
                cbAccion.SelectedIndex = -1; // limpia selección tras agregar
            }
        }

        private bool AgregarAccionSiNoExiste(string clave)
        {
            if (_accionesRapidas.Any(a => a.Clave == clave)) return false;

            var def = _catalogoAcciones.FirstOrDefault(x => x.Clave == clave);
            if (def == null) return false;

            var qa = BuildQuickAction(def);
            _accionesRapidas.Add(qa);
            return true;
        }

        private QuickAction BuildQuickAction(AccionDef def)
        {
            return new QuickAction
            {
                Clave = def.Clave,
                Titulo = def.Titulo,
                Icono = def.Icono,
                Ejecutar = def.Clave switch
                {
                    "NuevaVenta" => () => { new VentaWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "NuevaCompra" => () => { new CompraWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "AgregarProducto" => () =>
                    {
                        var win = new ProductoEdicionWindow(_conn);
                        if (win.ShowDialog() == true)
                        {
                            _productoService.AgregarProducto(win.Producto);
                        }
                        _ = RefreshAsync();
                    }
                    ,
                    "NuevoProveedor" => () => { new ProveedorWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Productos" => () => { new ProductoWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Ventas" => () => { new HistorialVentasWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Compras" => () => { new HistorialComprasWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Proveedores" => () => { new ProveedorWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Usuarios" => () => { new UsuarioWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Categorias" => () => { new CategoriaWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    "Dashboard" => () => { new DashboardWindow().ShowDialog(); _ = RefreshAsync(); }
                    ,
                    _ => () => { }
                }
            };
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is QuickAction qa)
            {
                qa.Ejecutar?.Invoke();
            }
        }

        private void QuickAction_Quitar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickAction qa)
            {
                _accionesRapidas.Remove(qa);
                GuardarAcciones();
            }
        }

        // Persistencia JSON en AppData
        private bool CargarAccionesGuardadas()
        {
            try
            {
                var file = AccionesFilePath;
                if (!File.Exists(file)) return false;

                var json = File.ReadAllText(file);
                var claves = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                bool added = false;
                foreach (var clave in claves)
                {
                    added |= AgregarAccionSiNoExiste(clave);
                }
                return added || claves.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void GuardarAcciones()
        {
            try
            {
                var dir = Path.GetDirectoryName(AccionesFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var claves = _accionesRapidas.Select(a => a.Clave).ToList();
                var json = JsonSerializer.Serialize(claves, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AccionesFilePath, json);
            }
            catch
            {
                // Ignorar fallos de escritura
            }
        }

        private void RefreshSync()
        {
            try
            {
                var start = DateTime.Today;
                var end = start.AddDays(1).AddTicks(-1);

                var productos = _productoService.ObtenerTodos();
                var ventasHoy = _ventaService.ObtenerTodas()
                    .Where(v => v.Estado == "Activa" && v.Fecha >= start && v.Fecha <= end).ToList();
                var comprasHoy = _compraService.ObtenerTodas()
                    .Where(c => c.Estado == "Activa" && c.Fecha >= start && c.Fecha <= end).ToList();

                txtTotalProductos.Text = productos.Count.ToString();
                txtStockBajo.Text = productos.Count(p => p.Stock <= UMBRAL_STOCK_BAJO).ToString();
                txtVentasHoy.Text = "S/ " + ventasHoy.Sum(v => v.Total).ToString("0.00");
                txtComprasHoy.Text = "S/ " + comprasHoy.Sum(c => c.Total).ToString("0.00");
                txtProductoMasVendido.Text = ObtenerProductoMasVendidoEnRango(start, end) ?? "Sin ventas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al refrescar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Tipos auxiliares
        private record AccionDef(string Clave, string Titulo, string Icono);

        private class QuickAction
        {
            public string Clave { get; set; } = "";
            public string Titulo { get; set; } = "";
            public string Icono { get; set; } = "\uE10F";
            public Action? Ejecutar { get; set; }
        }
    }
}