using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System_Market.Models;

namespace System_Market.Services
{
    public class ProductoService
    {
        private readonly string _connectionString;

        public ProductoService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Producto> ObtenerTodos()
        {
            var productos = new List<Producto>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"
                SELECT  p.Id, p.CodigoBarras, p.Nombre, p.CategoriaId, p.ProveedorId, 
                        p.PrecioCompra, p.PrecioVenta, p.Stock,
                        c.Nombre AS CategoriaNombre,
                        pr.Nombre AS ProveedorNombre
                FROM Productos p
                LEFT JOIN Categorias  c  ON p.CategoriaId  = c.Id
                LEFT JOIN Proveedores pr ON p.ProveedorId = pr.Id
                ORDER BY p.Nombre;";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                productos.Add(new Producto
                {
                    Id             = reader.GetInt32(0),
                    CodigoBarras   = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Nombre         = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    CategoriaId    = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    ProveedorId    = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    PrecioCompra   = reader.GetDecimal(5),
                    PrecioVenta    = reader.GetDecimal(6),
                    Stock          = reader.GetInt32(7),
                    CategoriaNombre= reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    ProveedorNombre= reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                });
            }
            return productos;
        }

        public void AgregarProducto(Producto producto)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"INSERT INTO Productos 
                (CodigoBarras, Nombre, CategoriaId, ProveedorId, PrecioCompra, PrecioVenta, Stock)
                VALUES (@CodigoBarras, @Nombre, @CategoriaId, @ProveedorId, @PrecioCompra, @PrecioVenta, @Stock)";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@CodigoBarras", (object?)producto.CodigoBarras ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nombre", producto.Nombre);
            cmd.Parameters.AddWithValue("@CategoriaId", (object?)producto.CategoriaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProveedorId", (object?)producto.ProveedorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrecioCompra", producto.PrecioCompra);
            cmd.Parameters.AddWithValue("@PrecioVenta", producto.PrecioVenta);
            cmd.Parameters.AddWithValue("@Stock", producto.Stock);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarProducto(Producto producto)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"UPDATE Productos SET 
                CodigoBarras = @CodigoBarras,
                Nombre       = @Nombre,
                CategoriaId  = @CategoriaId,
                ProveedorId  = @ProveedorId,
                PrecioCompra = @PrecioCompra,
                PrecioVenta  = @PrecioVenta,
                Stock        = @Stock
                WHERE Id     = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@CodigoBarras", (object?)producto.CodigoBarras ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nombre", producto.Nombre);
            cmd.Parameters.AddWithValue("@CategoriaId", (object?)producto.CategoriaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProveedorId", (object?)producto.ProveedorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrecioCompra", producto.PrecioCompra);
            cmd.Parameters.AddWithValue("@PrecioVenta", producto.PrecioVenta);
            cmd.Parameters.AddWithValue("@Stock", producto.Stock);
            cmd.Parameters.AddWithValue("@Id", producto.Id);
            cmd.ExecuteNonQuery();
        }

        public void EliminarProducto(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Productos WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public List<Producto> Filtrar(string texto)
        {
            var productos = new List<Producto>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var palabras = texto?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var condiciones = new List<string>();
            var parametros = new List<SQLiteParameter>();
            int i = 0;
            foreach (var palabra in palabras)
            {
                string paramNombre = $"@p{i}";
                condiciones.Add($"(p.Nombre LIKE {paramNombre} OR p.CodigoBarras LIKE {paramNombre})");
                parametros.Add(new SQLiteParameter(paramNombre, $"%{palabra}%"));
                i++;
            }

            string where = condiciones.Count > 0 ? "WHERE " + string.Join(" AND ", condiciones) : "";
            string query = $@"
                SELECT  p.Id, p.CodigoBarras, p.Nombre, p.CategoriaId, p.ProveedorId, 
                        p.PrecioCompra, p.PrecioVenta, p.Stock,
                        c.Nombre AS CategoriaNombre,
                        pr.Nombre AS ProveedorNombre
                FROM Productos p
                LEFT JOIN Categorias  c  ON p.CategoriaId  = c.Id
                LEFT JOIN Proveedores pr ON p.ProveedorId = pr.Id
                {where}
                ORDER BY p.Nombre";

            using var cmd = new SQLiteCommand(query, connection);
            foreach (var param in parametros) cmd.Parameters.Add(param);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                productos.Add(new Producto
                {
                    Id              = reader.GetInt32(0),
                    CodigoBarras    = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Nombre          = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    CategoriaId     = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    ProveedorId     = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    PrecioCompra    = reader.GetDecimal(5),
                    PrecioVenta     = reader.GetDecimal(6),
                    Stock           = reader.GetInt32(7),
                    CategoriaNombre = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    ProveedorNombre = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                });
            }
            return productos;
        }

        public Producto? ObtenerPorCodigoBarras(string codigoBarras)
        {
            if (string.IsNullOrWhiteSpace(codigoBarras)) return null;

            string original = codigoBarras.Trim();
            string sinCeros = original.TrimStart('0');
            string padded13 = (sinCeros.All(char.IsDigit) && sinCeros.Length > 0 && sinCeros.Length < 13)
                ? sinCeros.PadLeft(13, '0')
                : sinCeros;

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            string query = @"
                SELECT Id, CodigoBarras, Nombre, CategoriaId, ProveedorId, PrecioCompra, PrecioVenta, Stock
                FROM Productos
                WHERE TRIM(CodigoBarras) IN (@c1, @c2, @c3)
                LIMIT 1;";

            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@c1", original);
            cmd.Parameters.AddWithValue("@c2", sinCeros);
            cmd.Parameters.AddWithValue("@c3", padded13);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Producto
                {
                    Id           = reader.GetInt32(0),
                    CodigoBarras = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Nombre       = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    CategoriaId  = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    ProveedorId  = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    PrecioCompra = reader.GetDecimal(5),
                    PrecioVenta  = reader.GetDecimal(6),
                    Stock        = reader.GetInt32(7)
                };
            }
            return null;
        }
    }
}
