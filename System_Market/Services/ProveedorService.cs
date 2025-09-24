using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    // Servicio encargado de gestionar las operaciones CRUD para proveedores en la base de datos.
    // Permite obtener, agregar, actualizar y eliminar proveedores.
    public class ProveedorService
    {
        // Cadena de conexión a la base de datos SQLite.
        private readonly string _connectionString;

        // Constructor que recibe la cadena de conexión y la almacena para su uso en los métodos.
        public ProveedorService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Obtiene todos los proveedores registrados en la base de datos.
        // Devuelve una lista de objetos Proveedor con sus propiedades cargadas.
        public List<Proveedor> ObtenerTodos()
        {
            var proveedores = new List<Proveedor>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Proveedores";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // Por cada fila encontrada, crea una instancia de Proveedor y la agrega a la lista.
                proveedores.Add(new Proveedor
                {
                    Id = reader.GetInt32(0), // Columna Id
                    Nombre = reader.GetString(1), // Columna Nombre
                    RUC = reader.IsDBNull(2) ? "" : reader.GetString(2), // Columna RUC (puede ser nulo)
                    Telefono = reader.IsDBNull(3) ? "" : reader.GetString(3) // Columna Telefono (puede ser nulo)
                });
            }
            return proveedores;
        }

        // Inserta un nuevo proveedor en la base de datos.
        // Recibe un objeto Proveedor con los datos a registrar.
        public void AgregarProveedor(Proveedor proveedor)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"INSERT INTO Proveedores (Nombre, RUC, Telefono)
                             VALUES (@Nombre, @RUC, @Telefono)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", proveedor.Nombre);
            cmd.Parameters.AddWithValue("@RUC", (object?)proveedor.RUC ?? "");
            cmd.Parameters.AddWithValue("@Telefono", (object?)proveedor.Telefono ?? "");
            cmd.ExecuteNonQuery();
        }

        // Actualiza los datos de un proveedor existente en la base de datos.
        // Se identifica el proveedor por su Id.
        public void ActualizarProveedor(Proveedor proveedor)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"UPDATE Proveedores SET 
                                Nombre = @Nombre,
                                RUC = @RUC,
                                Telefono = @Telefono
                             WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Nombre", proveedor.Nombre);
            cmd.Parameters.AddWithValue("@RUC", (object?)proveedor.RUC ?? "");
            cmd.Parameters.AddWithValue("@Telefono", (object?)proveedor.Telefono ?? "");
            cmd.Parameters.AddWithValue("@Id", proveedor.Id);
            cmd.ExecuteNonQuery();
        }

        // Elimina un proveedor de la base de datos según su Id.
        // Si el proveedor está relacionado con productos o compras, puede lanzar una excepción por restricción de clave foránea.
        public void EliminarProveedor(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Proveedores WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}