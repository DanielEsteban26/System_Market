using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    // Servicio encargado de gestionar las operaciones de compras en la base de datos.
    // Permite registrar, actualizar, anular y consultar compras y sus detalles.
    public class CompraService
    {
        // Cadena de conexión a la base de datos SQLite.
        private readonly string _connectionString;

        // Constructor que recibe la cadena de conexión.
        public CompraService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Obtiene todas las compras registradas, incluyendo el nombre del usuario y proveedor.
        public List<Compra> ObtenerTodas()
        {
            var compras = new List<Compra>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"
                 SELECT c.Id, c.UsuarioId, c.ProveedorId, c.Fecha, c.Total, c.Estado, c.MotivoAnulacion,
                    u.Nombre as UsuarioNombre, p.Nombre as ProveedorNombre
                 FROM Compras c
                 JOIN Usuarios u ON c.UsuarioId = u.Id
                 JOIN Proveedores p ON c.ProveedorId = p.Id";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // Por cada compra encontrada, crea una instancia de Compra y la agrega a la lista.
                compras.Add(new Compra
                {
                    Id = reader.GetInt32(0),
                    UsuarioId = reader.GetInt32(1),
                    ProveedorId = reader.GetInt32(2),
                    Fecha = reader.GetDateTime(3),
                    Total = reader.GetDecimal(4),
                    Estado = reader.GetString(5),
                    MotivoAnulacion = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    UsuarioNombre = reader.GetString(7),
                    ProveedorNombre = reader.GetString(8)
                });
            }
            return compras;
        }

        // Registra una compra con sus detalles y actualiza el stock de los productos en una sola transacción.
        // Devuelve el Id de la compra registrada.
        public int AgregarCompraConDetalles(Compra compra, List<DetalleCompra> detalles)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Calcula el total de la compra sumando los subtotales de cada detalle.
                compra.Total = 0;
                foreach (var d in detalles)
                {
                    d.Subtotal = d.Cantidad * d.PrecioUnitario;
                    compra.Total += d.Subtotal;
                }

                // Inserta la compra principal y obtiene el Id generado.
                string queryCompra = @"INSERT INTO Compras (UsuarioId, ProveedorId, Fecha, Total, Estado, MotivoAnulacion)
                               VALUES (@UsuarioId, @ProveedorId, @Fecha, @Total, @Estado, @MotivoAnulacion);
                               SELECT last_insert_rowid();";
                using var cmdCompra = new SQLiteCommand(queryCompra, connection, transaction);
                cmdCompra.Parameters.AddWithValue("@UsuarioId", compra.UsuarioId);
                cmdCompra.Parameters.AddWithValue("@ProveedorId", compra.ProveedorId);
                cmdCompra.Parameters.AddWithValue("@Fecha", compra.Fecha);
                cmdCompra.Parameters.AddWithValue("@Total", compra.Total);
                cmdCompra.Parameters.AddWithValue("@Estado", compra.Estado ?? "Activa");
                cmdCompra.Parameters.AddWithValue("@MotivoAnulacion", compra.MotivoAnulacion ?? "");
                int compraId = Convert.ToInt32(cmdCompra.ExecuteScalar());

                // Inserta cada detalle de la compra y actualiza el stock del producto correspondiente.
                foreach (var detalle in detalles)
                {
                    string queryDetalle = @"INSERT INTO DetalleCompras (CompraId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
                                    VALUES (@CompraId, @ProductoId, @Cantidad, @PrecioUnitario, @Subtotal)";
                    using var cmdDetalle = new SQLiteCommand(queryDetalle, connection, transaction);
                    cmdDetalle.Parameters.AddWithValue("@CompraId", compraId);
                    cmdDetalle.Parameters.AddWithValue("@ProductoId", detalle.ProductoId);
                    cmdDetalle.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                    cmdDetalle.Parameters.AddWithValue("@PrecioUnitario", detalle.PrecioUnitario);
                    cmdDetalle.Parameters.AddWithValue("@Subtotal", detalle.Subtotal);
                    cmdDetalle.ExecuteNonQuery();

                    // Aumenta el stock del producto comprado.
                    string queryStock = @"UPDATE Productos SET Stock = Stock + @Cantidad WHERE Id = @ProductoId";
                    using var cmdStock = new SQLiteCommand(queryStock, connection, transaction);
                    cmdStock.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                    cmdStock.Parameters.AddWithValue("@ProductoId", detalle.ProductoId);
                    cmdStock.ExecuteNonQuery();
                }

                transaction.Commit();
                return compraId;
            }
            catch
            {
                // Si ocurre un error, revierte toda la transacción.
                transaction.Rollback();
                throw;
            }
        }

        // Actualiza los datos de una compra existente en la base de datos.
        public void ActualizarCompra(Compra compra)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"UPDATE Compras SET 
                        UsuarioId = @UsuarioId,
                        ProveedorId = @ProveedorId,
                        Fecha = @Fecha,
                        Total = @Total,
                        Estado = @Estado,
                        MotivoAnulacion = @MotivoAnulacion
                     WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UsuarioId", compra.UsuarioId);
            cmd.Parameters.AddWithValue("@ProveedorId", compra.ProveedorId);
            cmd.Parameters.AddWithValue("@Fecha", compra.Fecha);
            cmd.Parameters.AddWithValue("@Total", compra.Total);
            cmd.Parameters.AddWithValue("@Estado", compra.Estado ?? "Activa");
            cmd.Parameters.AddWithValue("@MotivoAnulacion", compra.MotivoAnulacion ?? "");
            cmd.Parameters.AddWithValue("@Id", compra.Id);
            cmd.ExecuteNonQuery();
        }

        // Obtiene todos los detalles de una compra específica, incluyendo el nombre del producto.
        public List<DetalleCompra> ObtenerDetallesPorCompra(int compraId)
        {
            var detalles = new List<DetalleCompra>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"
            SELECT d.Id, d.CompraId, d.ProductoId, d.Cantidad, d.PrecioUnitario, d.Subtotal,
               p.Nombre as ProductoNombre
            FROM DetalleCompras d
            JOIN Productos p ON d.ProductoId = p.Id
            WHERE d.CompraId = @CompraId";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@CompraId", compraId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                detalles.Add(new DetalleCompra
                {
                    Id = reader.GetInt32(0),
                    CompraId = reader.GetInt32(1),
                    ProductoId = reader.GetInt32(2),
                    Cantidad = reader.GetInt32(3),
                    PrecioUnitario = reader.GetDecimal(4),
                    Subtotal = reader.GetDecimal(5),
                    ProductoNombre = reader.GetString(6)
                });
            }
            return detalles;
        }

        // Anula una compra y revierte el stock de los productos involucrados.
        // Cambia el estado de la compra a "Anulada" y descuenta del stock las cantidades compradas.
        public void AnularCompra(int compraId, string motivo)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Cambia el estado de la compra a "Anulada" y guarda el motivo.
                string query = @"UPDATE Compras SET Estado = 'Anulada', MotivoAnulacion = @Motivo WHERE Id = @Id";
                using var cmd = new SQLiteCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("@Motivo", motivo);
                cmd.Parameters.AddWithValue("@Id", compraId);
                cmd.ExecuteNonQuery();

                // Obtiene los detalles de la compra anulada.
                string queryDetalles = "SELECT ProductoId, Cantidad FROM DetalleCompras WHERE CompraId = @CompraId";
                using var cmdDetalles = new SQLiteCommand(queryDetalles, connection, transaction);
                cmdDetalles.Parameters.AddWithValue("@CompraId", compraId);
                using var reader = cmdDetalles.ExecuteReader();
                var detalles = new List<(int ProductoId, int Cantidad)>();
                while (reader.Read())
                {
                    detalles.Add((reader.GetInt32(0), reader.GetInt32(1)));
                }

                // Por cada producto, descuenta del stock la cantidad que había sido sumada al comprar.
                foreach (var d in detalles)
                {
                    string queryStock = @"UPDATE Productos SET Stock = Stock - @Cantidad WHERE Id = @ProductoId";
                    using var cmdStock = new SQLiteCommand(queryStock, connection, transaction);
                    cmdStock.Parameters.AddWithValue("@Cantidad", d.Cantidad);
                    cmdStock.Parameters.AddWithValue("@ProductoId", d.ProductoId);
                    cmdStock.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                // Si ocurre un error, revierte toda la transacción.
                transaction.Rollback();
                throw;
            }
        }
    }
}