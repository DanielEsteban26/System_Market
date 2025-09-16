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
    public partial class LoginWindow : Window
    {
        private static bool _isOpen;

        public Usuario? UsuarioLogueado { get; private set; }

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

            Loaded += LoginWindow_Loaded;
            Closed += (_, __) => { _isOpen = false; Debug.WriteLine("LoginWindow.Closed"); };
        }

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

                string query = "SELECT Id, Nombre, Usuario, Clave, Rol FROM Usuarios WHERE Usuario = @usuario AND Clave = @clave LIMIT 1";
                using var cmd = new SQLiteCommand(query, connection);
                cmd.Parameters.AddWithValue("@usuario", usuario);
                cmd.Parameters.AddWithValue("@clave", clave);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Debug.WriteLine("LoginWindow - credentials OK, set DialogResult = true");
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

                MessageBox.Show("Usuario o contraseña incorrectos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtUsuario.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar con la base de datos:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        // Manejador para abrir enlaces (correo / web)
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
    }
}