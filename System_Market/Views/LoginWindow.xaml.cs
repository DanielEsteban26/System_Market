// Referencias a librerías base, WPF y utilidades del sistema
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System_Market.Data;
using System_Market.Models;

namespace System_Market.Views
{
    // Ventana de inicio de sesión del sistema.
    // Permite autenticar usuarios y acceder al sistema según su rol.
    public partial class LoginWindow : Window
    {
        // Controla que solo exista una instancia de la ventana de login abierta a la vez
        private static bool _isOpen;

        // Usuario autenticado tras un login exitoso
        public Usuario? UsuarioLogueado { get; private set; }

        // Constructor: inicializa la ventana y previene instancias duplicadas
        public LoginWindow()
        {
            if (_isOpen)
            {
                Debug.WriteLine("LoginWindow ctor: already open, throwing");
                Close(); // no crear otra
                return;
            }
            _isOpen = true;
            Debug.WriteLine("LoginWindow.ctor - new instance");
            InitializeComponent();

            // Eventos de ciclo de vida de la ventana
            Loaded += LoginWindow_Loaded;
            Closed += (_, __) => { _isOpen = false; Debug.WriteLine("LoginWindow.Closed"); };
        }

        // Evento: al cargar la ventana, enfoca el campo usuario y registra eventos de teclado
        private void LoginWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("LoginWindow.Loaded");
            txtUsuario.Focus();

            // Registrar manejo de Enter en los campos de usuario/clave
            try
            {
                txtUsuario.KeyDown += TxtInput_KeyDown;
                txtPassword.KeyDown += TxtInput_KeyDown;
            }
            catch
            {
                // Si por alguna razón los controles no existen, no romper la ventana.
            }
        }

        // Permite iniciar sesión presionando Enter en los campos de usuario o contraseña
        private void TxtInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var usuario = txtUsuario.Text.Trim();
            var clave = txtPassword.Password;

            // Si no hay usuario, colocar foco en usuario
            if (string.IsNullOrEmpty(usuario))
            {
                txtUsuario.Focus();
                e.Handled = true;
                return;
            }

            // Si no hay clave, colocar foco en contraseña
            if (string.IsNullOrEmpty(clave))
            {
                txtPassword.Focus();
                e.Handled = true;
                return;
            }

            // Ambos campos completados -> ejecutar login
            e.Handled = true;
            BtnIngresar_Click(this, new RoutedEventArgs());
        }

        // Lógica principal de autenticación: valida usuario y contraseña contra la base de datos
        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string clave = txtPassword.Password;

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            {
                MessageBox.Show("Ingrese usuario y contraseña.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var connection = new SQLiteConnection(DatabaseInitializer.GetConnectionString());
                connection.Open();

                // Consulta SQL para buscar el usuario con las credenciales ingresadas
                string query = "SELECT Id, Nombre, Usuario, Clave, Rol FROM Usuarios WHERE Usuario = @usuario AND Clave = @clave LIMIT 1";
                using var cmd = new SQLiteCommand(query, connection);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@clave", clave);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Debug.WriteLine("LoginWindow - credentials OK, set DialogResult = true");
                    // Si las credenciales son correctas, crea el objeto Usuario y lo guarda en sesión
                    UsuarioLogueado = new Usuario
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        UsuarioNombre = reader.GetString(2),
                        Clave = reader.GetString(3),
                        Rol = reader.GetString(4)
                    };

                    // Guardar en sesión global para que otras ventanas puedan leerlo
                    SesionActual.Usuario = UsuarioLogueado;

                    DialogResult = true;
                    return;
                }

                // Si no se encontró el usuario, muestra error y limpia la contraseña
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtUsuario.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar con la base de datos:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Abre la ventana de registro de usuario como modal
        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            var reg = new RegisterWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // Abrir modal de registro; no recalculamos visibilidad aquí porque ahora siempre debe mostrarse.
            reg.ShowDialog();
        }

        // Manejador para abrir enlaces (correo / web) desde la ventana
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir el enlace: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        // Cierra la ventana de login
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Permite arrastrar la ventana haciendo clic en el borde superior
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { this.DragMove(); } catch { /* evitar excepción si no se puede arrastrar */ }
            }
        }

        // Permite cerrar la ventana presionando la tecla Escape
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}