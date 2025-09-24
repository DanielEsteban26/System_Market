using System;
using System.Data.SQLite;
using System.Windows;
using System_Market.Data;
using System_Market.Services;

namespace System_Market.Views
{
    // Ventana para registrar nuevos usuarios o el primer administrador.
    // Controla la lógica de seguridad para la creación inicial y posteriores usuarios.
    public partial class RegisterWindow : Window
    {
        // Indica si se está creando el primer administrador (sin clave previa)
        private bool isFirstAdministrator = false;

        public RegisterWindow()
        {
            InitializeComponent();

            try
            {
                using var conn = new SQLiteConnection(DatabaseInitializer.GetConnectionString());
                conn.Open();

                // Contar administradores existentes
                using var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Usuarios WHERE Rol = 'Administrador'", conn);
                var countObj = cmd.ExecuteScalar();
                int count = 0;
                if (countObj != null && int.TryParse(countObj.ToString(), out int parsed)) count = parsed;

                // Obtener número de ejecuciones para la lógica de las primeras 3 ejecuciones
                int execCount = ExecutionCounterService.GetExecutionCount();

                // Permitir creación sin clave admin si:
                // - no hay administradores y
                // - la aplicación está dentro de sus primeras 3 ejecuciones
                isFirstAdministrator = (count == 0) && (execCount <= 3);

                if (isFirstAdministrator)
                {
                    // Permitir crear el primer administrador sin clave previa
                    txtAdminClave.Visibility = Visibility.Collapsed;
                    txtInfo.Text = "No existe un Administrador. Durante las primeras 3 ejecuciones puedes crear el primer Administrador sin clave.";
                    this.Title = "Registrar Administrador (Primera vez)";
                }
                else if (count == 0)
                {
                    // No hay administradores pero ya pasaron las primeras 3 ejecuciones
                    txtAdminClave.Visibility = Visibility.Visible;
                    txtInfo.Text = "No existe un Administrador, pero ya pasaron las primeras 3 ejecuciones. Se requiere clave de Administrador para crear usuarios.";
                    this.Title = "Registrar Usuario (requiere autorización)";
                }
                else
                {
                    // Ya existe al menos un administrador
                    txtAdminClave.Visibility = Visibility.Visible;
                    txtInfo.Text = "Para crear un nuevo usuario se requiere la clave de un Administrador existente.";
                    this.Title = "Registrar Usuario (requiere autorización)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al verificar administradores: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // En caso de error, mostrar campo de clave admin por seguridad
                txtAdminClave.Visibility = Visibility.Visible;
                txtInfo.Text = "No se pudo verificar administradores. Se requiere clave de Administrador para continuar.";
                isFirstAdministrator = false;
            }

            // Al cargar, enfoca el campo nombre
            Loaded += (_, __) => txtNombre.Focus();
        }

        // Maneja el evento de registro de usuario o administrador
        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text.Trim();
            string usuario = txtUsuario.Text.Trim();
            string clave = txtClave.Password.Trim();

            // Validación de campos obligatorios
            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            {
                MessageBox.Show("Completa todos los campos.", "Registro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validación de longitud mínima de clave
            if (clave.Length < 6)
            {
                MessageBox.Show("La clave debe tener al menos 6 caracteres.", "Registro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var conn = new SQLiteConnection(DatabaseInitializer.GetConnectionString());
                conn.Open();

                if (isFirstAdministrator)
                {
                    // Crear el primer Administrador
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO Usuarios (Nombre, Usuario, Clave, Rol) VALUES (@n, @u, @c, @r)";
                    cmd.Parameters.AddWithValue("@n", nombre);
                    cmd.Parameters.AddWithValue("@u", usuario);
                    cmd.Parameters.AddWithValue("@c", clave);
                    cmd.Parameters.AddWithValue("@r", "Administrador");

                    cmd.ExecuteNonQuery();

                    MessageBox.Show($"Se creó '{nombre}' como Administrador.", "Registro", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                    return;
                }
                else
                {
                    // Validar clave de algún Administrador existente
                    string adminClave = txtAdminClave.Password.Trim();
                    if (string.IsNullOrEmpty(adminClave))
                    {
                        MessageBox.Show("Se requiere la clave de un Administrador para crear usuarios.", "Autorización requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                        this.DialogResult = false;
                        this.Close();
                        return;
                    }

                    using var cmdCheck = conn.CreateCommand();
                    cmdCheck.CommandText = "SELECT 1 FROM Usuarios WHERE Rol = 'Administrador' AND Clave = @adminClave LIMIT 1";
                    cmdCheck.Parameters.AddWithValue("@adminClave", adminClave);

                    var exists = cmdCheck.ExecuteScalar();
                    bool adminValid = exists != null;

                    if (!adminValid)
                    {
                        MessageBox.Show("Clave de administrador incorrecta. No se creó el usuario.", "Autorización fallida", MessageBoxButton.OK, MessageBoxImage.Error);
                        this.DialogResult = false;
                        this.Close();
                        return;
                    }

                    // Si la clave admin es correcta, crear el nuevo usuario con rol Cajero por defecto
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO Usuarios (Nombre, Usuario, Clave, Rol) VALUES (@n, @u, @c, @r)";
                    cmd.Parameters.AddWithValue("@n", nombre);
                    cmd.Parameters.AddWithValue("@u", usuario);
                    cmd.Parameters.AddWithValue("@c", clave);
                    cmd.Parameters.AddWithValue("@r", "Cajero");

                    try
                    {
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Usuario creado correctamente.", "Registro", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.DialogResult = true;
                        this.Close();
                        return;
                    }
                    catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
                    {
                        MessageBox.Show("El nombre de usuario ya existe. Elige otro usuario.", "Registro", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // permitir corrección sin cerrar
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar: " + ex.Message, "Registro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Cancela el registro y cierra la ventana
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}