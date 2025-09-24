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

// Clase principal de la aplicación WPF. Inicializa la base de datos, configura la cultura y gestiona el ciclo de vida de login y ventana principal.
public partial class App : Application
{
    private bool _loginShown;

    // Se ejecuta al iniciar la aplicación
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Inicializar la base de datos solo una vez
        DatabaseInitializer.InitializeDatabase();

        // Incrementar contador de ejecuciones (para estadísticas o control de uso)
        ExecutionCounterService.IncrementExecutionCount();

        // Configuración de cultura regional (Perú, separador decimal coma, símbolo S/)
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

        // Evita que la app se cierre al cerrar el login (se controla explícitamente)
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
                // Cerrar cualquier otra ventana de login que haya quedado abierta
                foreach (var w in Application.Current.Windows.OfType<LoginWindow>().ToList())
                {
                    try { if (w != login) w.Close(); } catch { }
                }

                // Mostrar la ventana principal, pasando el usuario autenticado
                var main = new MainWindow(login.UsuarioLogueado);
                this.MainWindow = main;
                main.Show();

                // Cambiar el modo de cierre: ahora la app se cierra al cerrar la ventana principal
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            else
            {
                // Si el login fue cancelado o fallido, cerrar la aplicación
                Shutdown();
            }
        }
        else
        {
            // Seguridad: si por alguna razón se reentra, evitar abrir más logins
            Shutdown();
        }
    }
}