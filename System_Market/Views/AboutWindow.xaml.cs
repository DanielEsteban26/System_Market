using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace System_Market.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            // Mostrar versión real del ensamblado
            try
            {
                var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
                txtVersion.Text = $"Versión {ver}";
            }
            catch { /* Silenciar errores */ }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}