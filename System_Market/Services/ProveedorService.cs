using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    public class ProveedorService
    {
        private readonly string _connectionString;

        public ProveedorService(string connectionString)
        {
            _connectionString = connectionString;
        }

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
                proveedores.Add(new Proveedor
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    RUC = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Telefono = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
            return proveedores;
        }

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
