using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    public class UsuarioService
    {
        private readonly string _connectionString;

        public UsuarioService(string connectionString)
        {
            _connectionString = connectionString;
        }

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

        public void AgregarUsuario(Usuario usuario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Validar que no exista el usuario
            string checkQuery = "SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario";
            using (var checkCmd = new SQLiteCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
                long count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                    throw new SQLiteException("El nombre de usuario ya existe.");
            }

            string query = @"INSERT INTO Usuarios (Nombre, Usuario, Clave, Rol)
                     VALUES (@Nombre, @Usuario, @Clave, @Rol)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
            cmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
            cmd.Parameters.AddWithValue("@Clave", usuario.Clave);
            cmd.Parameters.AddWithValue("@Rol", usuario.Rol);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarUsuario(Usuario usuario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Validar que no exista el usuario en otro Id
            string checkQuery = "SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario AND Id != @Id";
            using (var checkCmd = new SQLiteCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@Usuario", usuario.UsuarioNombre);
                checkCmd.Parameters.AddWithValue("@Id", usuario.Id);
                long count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                    throw new SQLiteException("El nombre de usuario ya existe.");
            }

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

        public void EliminarUsuario(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Usuarios WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

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
