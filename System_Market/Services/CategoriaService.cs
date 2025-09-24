using System.Collections.Generic;
using System.Data.SQLite;
using System_Market.Models;
using System_Market.Data;

namespace System_Market.Services
{
    // Servicio encargado de gestionar las operaciones CRUD para la entidad Categoria en la base de datos.
    // Permite obtener, agregar, actualizar y eliminar categorías de productos.
    public class CategoriaService
    {
        // Cadena de conexión a la base de datos SQLite.
        private readonly string _connectionString;

        // Constructor que recibe la cadena de conexión y la almacena para su uso en los métodos.
        public CategoriaService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Obtiene todas las categorías registradas en la base de datos.
        // Devuelve una lista de objetos Categoria con sus propiedades cargadas.
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
                // Por cada fila encontrada, crea una instancia de Categoria y la agrega a la lista.
                categorias.Add(new Categoria
                {
                    Id = reader.GetInt32(0),      // Columna Id
                    Nombre = reader.GetString(1)  // Columna Nombre
                });
            }
            return categorias;
        }

        // Inserta una nueva categoría en la base de datos.
        // Recibe un objeto Categoria con el nombre a registrar.
        public void AgregarCategoria(Categoria categoria)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "INSERT INTO Categorias (Nombre) VALUES (@Nombre)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", categoria.Nombre);
            cmd.ExecuteNonQuery();
        }

        // Actualiza el nombre de una categoría existente en la base de datos.
        // Se identifica la categoría por su Id.
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

        // Elimina una categoría de la base de datos según su Id.
        // Si la categoría está relacionada con productos, puede lanzar una excepción por restricción de clave foránea.
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