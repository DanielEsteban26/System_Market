using System.Globalization;
using System.Threading;
using System.Windows;
using System_Market.Data;
using System.Configuration;
using System.Data;

namespace System_Market;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var ci = new CultureInfo("es-PE"); // o "es-ES" si prefieres
        ci.NumberFormat.CurrencySymbol = "S/";
        ci.NumberFormat.NumberDecimalSeparator = ",";
        ci.NumberFormat.CurrencyDecimalSeparator = ",";
        ci.NumberFormat.NumberGroupSeparator = ".";
        Thread.CurrentThread.CurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;

        // Para que los bindings usen esta cultura
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage(ci.IetfLanguageTag)));

        // Llamamos al inicializador para crear carpeta + DB + tablas
        DatabaseInitializer.InitializeDatabase();
    }
}

