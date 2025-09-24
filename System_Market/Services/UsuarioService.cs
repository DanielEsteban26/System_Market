using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    // Servicio encargado de gestionar las operaciones CRUD y autenticación para usuarios en la base de datos.
    public class UsuarioService
    {
        // Cadena de conexión a la base de datos SQLite.
        private readonly string _connectionString;

        // Constructor que recibe la cadena de conexión.
        public UsuarioService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Obtiene todos los usuarios registrados en la base de datos.
        // Devuelve una lista de objetos Usuario con sus propiedades cargadas.
        public List<Usuario> ObtenerTodos()
        {
            var usuarios = new List<Usuario>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Usuarios";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                usuarios.Add(new Usuario
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    UsuarioNombre = reader.GetString(2),
                    Clave = reader.GetString(3),
                    Rol = reader.GetString(4)
                });
            }
            return usuarios;
        }

        // Agrega un nuevo usuario a la base de datos.
        // Valida que no exista otro usuario con el mismo nombre de usuario.
        public void AgregarUsuario(Usuario usuario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Verifica que el nombre de usuario no exista ya en la base de datos.
            string checkQuery = "SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario";
            using (var checkCmd = new SQLiteCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
                long count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                    throw new SQLiteException("El nombre de usuario ya existe.");
            }

            // Inserta el nuevo usuario.
            string query = @"INSERT INTO Usuarios (Nombre, Usuario, Clave, Rol)
                     VALUES (@Nombre, @Usuario, @Clave, @Rol)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
            cmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
            cmd.Parameters.AddWithValue("@Clave", usuario.Clave);
            cmd.Parameters.AddWithValue("@Rol", usuario.Rol);
            cmd.ExecuteNonQuery();
        }

        // Actualiza los datos de un usuario existente.
        // Valida que el nombre de usuario no exista en otro usuario diferente.
        public void ActualizarUsuario(Usuario usuario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Verifica que el nombre de usuario no esté repetido en otro Id.
            string checkQuery = "SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario AND Id != @Id";
            using (var checkCmd = new SQLiteCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
                checkCmd.Parameters.AddWithValue("@Id", usuario.Id);
                long count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                    throw new SQLiteException("El nombre de usuario ya existe.");
            }

            // Actualiza los datos del usuario.
            string query = @"UPDATE Usuarios SET 
                        Nombre = @Nombre,
                        Usuario = @Usuario,
                        Clave = @Clave,
                        Rol = @Rol
                     WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
            cmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
            cmd.Parameters.AddWithValue("@Clave", usuario.Clave);
            cmd.Parameters.AddWithValue("@Rol", usuario.Rol);
            cmd.Parameters.AddWithValue("@Id", usuario.Id);
            cmd.ExecuteNonQuery();
        }

        // Elimina un usuario de la base de datos según su Id.
        public void EliminarUsuario(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Usuarios WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        // Valida las credenciales de usuario y contraseña.
        // Si son correctas, devuelve el objeto Usuario correspondiente; si no, devuelve null.
        public Usuario? ValidarLogin(string usuario, string clave)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Usuarios WHERE lower(Usuario) = lower(@Usuario) AND Clave = @Clave";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Usuario", usuario.Trim().ToLower());
            cmd.Parameters.AddWithValue("@Clave", clave.Trim());
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Usuario
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    UsuarioNombre = reader.GetString(2),
                    Clave = reader.GetString(3),
                    Rol = reader.GetString(4)
                };
            }

            return null;
        }
    }
}