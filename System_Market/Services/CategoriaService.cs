using System.Collections.Generic;
using System.Data.SQLite;
using System_Market.Models;
using System_Market.Data;


namespace System_Market.Services
{
    public class CategoriaService
    {
        private readonly string _connectionString;

        public CategoriaService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Categoria> ObtenerTodas()
        {
            var categorias = new List<Categoria>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Categorias";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                categorias.Add(new Categoria
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1)
                });
            }
            return categorias;
        }

        public void AgregarCategoria(Categoria categoria)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "INSERT INTO Categorias (Nombre) VALUES (@Nombre)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", categoria.Nombre);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarCategoria(Categoria categoria)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "UPDATE Categorias SET Nombre = @Nombre WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", categoria.Nombre);
            cmd.Parameters.AddWithValue("@Id", categoria.Id);
            cmd.ExecuteNonQuery();
        }

        public void EliminarCategoria(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Categorias WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}