using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System_Market.Data;
using System_Market.Services;
using System_Market.Views;

namespace System_Market;

public partial class App : Application
{
    private bool _loginShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Inicializar DB una sola vez
        DatabaseInitializer.InitializeDatabase();

        // Incrementar contador de ejecuciones
        ExecutionCounterService.IncrementExecutionCount();

        // Cultura
        var ci = new CultureInfo("es-PE");
        ci.NumberFormat.CurrencySymbol = "S/";
        ci.NumberFormat.NumberDecimalSeparator = ",";
        ci.NumberFormat.CurrencyDecimalSeparator = ",";
        ci.NumberFormat.NumberGroupSeparator = ".";
        Thread.CurrentThread.CurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage(ci.IetfLanguageTag)));

        // Evita que la app se cierre cuando se cierra el login
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Mostrar login modal solo una vez
        if (!_loginShown)
        {
            _loginShown = true;
            Debug.WriteLine("App.OnStartup - antes ShowDialog Login");
            var login = new LoginWindow();
            bool? result = login.ShowDialog();
            Debug.WriteLine("App.OnStartup - ShowDialog retornó: " + (result?.ToString() ?? "null"));

            if (result == true)
            {
                // Asegurarnos de que no quede ninguna LoginWindow abierta
                foreach (var w in Application.Current.Windows.OfType<LoginWindow>().ToList())
                {
                    try { if (w != login) w.Close(); } catch { }
                }

                // Mostrar MainWindow
                var main = new MainWindow(login.UsuarioLogueado);
                this.MainWindow = main;
                main.Show();

                // Iniciar servicios desde Loaded del MainWindow (ya lo haces ahí)
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            else
            {
                Shutdown();
            }
        }
        else
        {
            // si por alguna razón se llega aquí, evita abrir más logins
            Shutdown();
        }
    }
}

