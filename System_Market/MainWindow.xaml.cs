using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System_Market.Data;
using System_Market.Services;
using System_Market.Views;
using System_Market.Models;

namespace System_Market
{
    // Ventana principal del sistema: dashboard, accesos rápidos y gestión de entidades.
    // Controla la sesión, permisos, acciones rápidas y el flujo de escaneo de códigos.
    public partial class MainWindow : Window
    {
        private readonly string _conn;
        private readonly ProductoService _productoService;
        private readonly VentaService _ventaService;
        private readonly CompraService _compraService;
        private const int UMBRAL_STOCK_BAJO = 5;

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

        private string AccionesFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "System_Market", "quick_actions.json");

        // Constructor principal, recibe el usuario logueado (opcional)
        public MainWindow(Usuario? usuario = null)
        {
            InitializeComponent();
            Debug.WriteLine("MainWindow.ctor");

            _conn = DatabaseInitializer.GetConnectionString();
            _productoService = new ProductoService(_conn);
            _ventaService = new VentaService(_conn);
            _compraService = new CompraService(_conn);

            icQuickActions.ItemsSource = _accionesRapidas;
            cbAccion.ItemsSource = _catalogoAcciones;

            // Establece usuario en sesión y actualiza UI
            if (usuario != null)
            {
                System_Market.Models.SesionActual.Usuario = usuario;
                txtUsuarioActual.Text = usuario.Nombre ?? "Usuario";
                txtRolUsuario.Text = usuario.Rol ?? txtRolUsuario.Text;
            }
            else
            {
                txtUsuarioActual.Text = System_Market.Models.SesionActual.Usuario?.Nombre ?? "Usuario Demo";
                txtRolUsuario.Text = System_Market.Models.SesionActual.Usuario?.Rol ?? txtRolUsuario.Text;
            }

            AplicarPermisosMainWindow();

            // Cargar acciones rápidas guardadas o por defecto
            if (!CargarAccionesGuardadas())
            {
                AgregarAccionSiNoExiste("NuevaVenta");
                AgregarAccionSiNoExiste("AgregarProducto");
                AgregarAccionSiNoExiste("NuevoProveedor");
                GuardarAcciones();
            }

            this.Closed += (_, __) => {
                GuardarAcciones();
                try
                {
                    if (_scannerSuscrito)
                    {
                        System_Market.Services.BarcodeScannerService.OnCodeScanned -= HandleScannedCode;
                        _scannerSuscrito = false;
                    }
                }
                catch { }
            };

            // Suscribirse al evento del escáner solo una vez
            try
            {
                if (!_scannerSuscrito)
                {
                    System_Market.Services.BarcodeScannerService.OnCodeScanned += HandleScannedCode;
                    _scannerSuscrito = true;
                }
            }
            catch { }
        }

        // Evento Loaded: inicia el servicio de escáner y refresca el dashboard
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                BarcodeScannerService.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error iniciando BarcodeScannerService: " + ex.Message);
            }

            await RefreshAsync();
        }

        // Refresca los datos principales del dashboard
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

                txtVentasHoy.Text = CurrencyService.FormatSoles(result.ventas.Sum(v => v.Total));
                txtComprasHoy.Text = CurrencyService.FormatSoles(result.compras.Sum(c => c.Total));

                txtProductoMasVendido.Text = result.masVendido ?? "Sin ventas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al refrescar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Devuelve el producto más vendido en el rango dado
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

        // --- Acciones de botones principales ---
        private async void BtnRefrescarDashboard_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
        private async void BtnDashboard_Click(object sender, RoutedEventArgs e) { new DashboardWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnProductos_Click(object sender, RoutedEventArgs e) { new ProductoWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnVentas_Click(object sender, RoutedEventArgs e) { new VentaWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnCompras_Click(object sender, RoutedEventArgs e) { new CompraWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnProveedores_Click(object sender, RoutedEventArgs e) { new ProveedorWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnUsuarios_Click(object sender, RoutedEventArgs e) { new UsuarioWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnCategorias_Click(object sender, RoutedEventArgs e) { new CategoriaWindow().ShowDialog(); await RefreshAsync(); }
        private async void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new ReportesWindow
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                w.ShowDialog();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error abriendo Reportes: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Acciones rápidas: agregar, ejecutar y quitar
        private void BtnAgregarAccion_Click(object sender, RoutedEventArgs e)
        {
            if (cbAccion.SelectedValue is string clave)
            {
                if (AgregarAccionSiNoExiste(clave)) GuardarAcciones();
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

        private QuickAction BuildQuickAction(AccionDef def) => new QuickAction
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
                    if (win.ShowDialog() == true) _productoService.AgregarProducto(win.Producto);
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

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is QuickAction qa) qa.Ejecutar?.Invoke();
        }

        private void QuickAction_Quitar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickAction qa)
            {
                _accionesRapidas.Remove(qa);
                GuardarAcciones();
            }
        }

        // Persistencia de acciones rápidas
        private bool CargarAccionesGuardadas()
        {
            try
            {
                if (!File.Exists(AccionesFilePath)) return false;
                var json = File.ReadAllText(AccionesFilePath);
                var claves = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
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
                Directory.CreateDirectory(Path.GetDirectoryName(AccionesFilePath)!);
                var json = JsonSerializer.Serialize(_accionesRapidas.Select(a => a.Clave).ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AccionesFilePath, json);
            }
            catch { }
        }

        // Acerca de
        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var acerca = new AcercaWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            acerca.ShowDialog();
        }

        // Cerrar sesión: oculta main, muestra login modal y actúa según resultado
        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                var login = new LoginWindow();
                bool? result = login.ShowDialog();

                if (result == true && login.UsuarioLogueado != null)
                {
                    System_Market.Models.SesionActual.Usuario = login.UsuarioLogueado;
                    txtUsuarioActual.Text = login.UsuarioLogueado.Nombre;
                    txtRolUsuario.Text = login.UsuarioLogueado.Rol ?? txtRolUsuario.Text;
                    AplicarPermisosMainWindow();
                    await RefreshAsync();
                    this.Show();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cerrar sesión: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // Aplica permisos y visibilidad de controles según el rol del usuario
        private void AplicarPermisosMainWindow()
        {
            var admin = System_Market.Models.SesionActual.EsAdministrador();

            try { btnUsuarios.IsEnabled = admin; } catch { }
            try { btnCompras.IsEnabled = admin; } catch { }
            try { btnProveedores.IsEnabled = admin; } catch { }
            try { btnCategorias.IsEnabled = admin; } catch { }
            try { btnReportes.IsEnabled = admin; } catch { }
            try { btnUsuarios.Visibility = admin ? Visibility.Visible : Visibility.Collapsed; } catch { }

            if (!admin)
            {
                var permitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "NuevaVenta",
                    "AgregarProducto",
                    "Ventas"
                };

                _catalogoAcciones.RemoveAll(a => !permitidas.Contains(a.Clave));

                for (int i = _accionesRapidas.Count - 1; i >= 0; i--)
                {
                    if (!_catalogoAcciones.Any(c => c.Clave == _accionesRapidas[i].Clave))
                        _accionesRapidas.RemoveAt(i);
                }

                try { cbAccion.ItemsSource = null; cbAccion.ItemsSource = _catalogoAcciones; }
                catch { }
            }
        }

        // Maneja códigos escaneados desde el servicio de escáner (en hilo UI)
        public void HandleScannedCode(string codigo)
        {
            if (!this.IsActive || !this.IsVisible)
                return;

            if (string.IsNullOrWhiteSpace(codigo)) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    ProcesarCodigoBarras(codigo.Trim());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error procesando escaneo: " + ex.Message, "Escáner",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        private Queue<string> _pendingVentaCodes = new Queue<string>();

        // Procesa el código de barras escaneado y decide la acción según el rol y existencia del producto
        private void ProcesarCodigoBarras(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return;

            var producto = _productoService.ObtenerPorCodigoBarras(codigo);
            bool existeProducto = producto != null;

            var rol = System_Market.Models.SesionActual.Usuario?.Rol ?? string.Empty;
            var esAdmin = System_Market.Models.SesionActual.EsAdministrador();
            var esCajero = !esAdmin && (string.Equals(rol, "Cajero", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(rol, "Cajera", StringComparison.OrdinalIgnoreCase));

            // Si es cajero y el producto existe, ir directo a venta
            if (esCajero && existeProducto)
            {
                try
                {
                    new VentaWindow(codigo).ShowDialog();
                    _ = RefreshAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error abriendo ventana de venta: " + ex.Message, "Venta",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // Mostrar diálogo de acción según rol y existencia
            var dlg = new CodeActionDialogWindow(codigo, existeProducto, rol) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            bool handled = false;
            try
            {
                switch (dlg.OpcionSeleccionada)
                {
                    case CodeActionDialogWindow.OpcionSeleccionadaEnum.Venta:
                        new VentaWindow(codigo).ShowDialog();
                        handled = true;
                        break;

                    case CodeActionDialogWindow.OpcionSeleccionadaEnum.Compra:
                        new CompraWindow(codigo).ShowDialog();
                        handled = true;
                        break;

                    case CodeActionDialogWindow.OpcionSeleccionadaEnum.Crear:
                        var win = new ProductoEdicionWindow(_conn, producto: null, codigoPrefill: codigo, bloquearCodigo: true)
                        {
                            Owner = this,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        if (win.ShowDialog() == true && win.Producto != null)
                        {
                            _productoService.AgregarProducto(win.Producto);
                            handled = true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error ejecutando operación: " + ex.Message, "Operación",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!handled)
                {
                    var mainWin = Application.Current.Windows
                        .OfType<System_Market.MainWindow>()
                        .FirstOrDefault(w => w.IsVisible);

                    if (mainWin != null)
                    {
                        mainWin.HandleScannedCode(codigo);
                        handled = true;
                    }
                    else
                    {
                        if (_pendingVentaCodes.Count > 5) _pendingVentaCodes.Dequeue();
                        _pendingVentaCodes.Enqueue(codigo);
                    }
                }
                _ = RefreshAsync();
            }
        }

        private static bool _scannerSuscrito = false;

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