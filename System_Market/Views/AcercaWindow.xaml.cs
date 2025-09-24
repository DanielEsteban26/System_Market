using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace System_Market.Views
{
    public partial class AcercaWindow : Window
    {
        public AcercaWindow()
        {
            InitializeComponent();
            // Muestra la versión de la aplicación en el TextBlock correspondiente.
            txtVersion.Text = "Versión " + ObtenerVersion();
        }

        /// <summary>
        /// Obtiene la versión del ensamblado principal o ejecutable.
        /// Si falla, retorna "1.0.0.0" por defecto.
        /// </summary>
        private string ObtenerVersion()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                if (ver != null) return ver.ToString();
                var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                return fvi.FileVersion ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        /// <summary>
        /// Abre una URL en el navegador predeterminado o muestra un mensaje de error si falla.
        /// </summary>
        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir el enlace: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Evento: abre el repositorio de GitHub en el navegador.
        private void BtnGithub_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/DanielEsteban26/System_Market");
        }

        // Evento: abre el perfil de LinkedIn (puedes personalizar la URL).
        private void BtnLinkedIn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/"); // reemplaza por tu perfil si lo deseas
        }

        // Evento: abre el cliente de correo predeterminado al hacer clic en el enlace de email.
        private void Mail_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        // Evento: cierra la ventana "Acerca de".
        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Evento: al cargar la ventana, enfoca el botón "Cerrar" para accesibilidad.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try { btnCerrar?.Focus(); } catch { }
        }
    }
}