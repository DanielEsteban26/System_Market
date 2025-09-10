using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System_Market.Models;

namespace System_Market.Services
{
    public class VentaService
    {
        private readonly string _connectionString;

        public VentaService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Venta> ObtenerTodas()
        {
            var ventas = new List<Venta>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"
                SELECT v.Id, v.UsuarioId, v.Fecha, v.Total, v.Estado, v.MotivoAnulacion,
                       u.Nombre as UsuarioNombre
                FROM Ventas v
                JOIN Usuarios u ON v.UsuarioId = u.Id";
            using var cmd = new SQLiteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ventas.Add(new Venta
                {
                    Id = reader.GetInt32(0),
                    UsuarioId = reader.GetInt32(1),
                    Fecha = reader.GetDateTime(2),
                    Total = reader.GetDecimal(3),
                    Estado = reader.GetString(4),
                    MotivoAnulacion = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    UsuarioNombre = reader.GetString(6)
                });
            }
            return ventas;
        }

        /// <summary>
        /// Registra la venta, sus detalles y descuenta el stock en una sola transacción.
        /// </summary>
        public int AgregarVentaConDetalles(Venta venta, List<DetalleVenta> detalles)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Validar stock antes de registrar la venta
                foreach (var detalle in detalles)
                {
                    string queryStock = "SELECT Stock FROM Productos WHERE Id = @ProductoId";
                    using var cmdStock = new SQLiteCommand(queryStock, connection, transaction);
                    cmdStock.Parameters.AddWithValue("@ProductoId", detalle.ProductoId);
                    int stockActual = Convert.ToInt32(cmdStock.ExecuteScalar());

                    if (detalle.Cantidad > stockActual)
                    {
                        throw new InvalidOperationException(
                            $"No hay suficiente stock para el producto '{detalle.ProductoNombre}'. Stock actual: {stockActual}, solicitado: {detalle.Cantidad}");
                    }
                }
                // Calcular total
                venta.Total = 0;
                foreach (var d in detalles)
                {
                    d.Subtotal = d.Cantidad * d.PrecioUnitario;
                    venta.Total += d.Subtotal;
                }

                // Insertar la venta (cabecera)
                string queryVenta = @"INSERT INTO Ventas (UsuarioId, Fecha, Total, Estado, MotivoAnulacion)
                                      VALUES (@UsuarioId, @Fecha, @Total, @Estado, @MotivoAnulacion);
                                      SELECT last_insert_rowid();";
                using var cmdVenta = new SQLiteCommand(queryVenta, connection, transaction);
                cmdVenta.Parameters.AddWithValue("@UsuarioId", venta.UsuarioId);
                cmdVenta.Parameters.AddWithValue("@Fecha", venta.Fecha);
                cmdVenta.Parameters.AddWithValue("@Total", venta.Total);
                cmdVenta.Parameters.AddWithValue("@Estado", venta.Estado ?? "Activa");
                cmdVenta.Parameters.AddWithValue("@MotivoAnulacion", venta.MotivoAnulacion ?? "");
                int ventaId = Convert.ToInt32(cmdVenta.ExecuteScalar());

                // Insertar los detalles y actualizar stock
                foreach (var detalle in detalles)
                {
                    string queryDetalle = @"INSERT INTO DetalleVentas (VentaId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
                                            VALUES (@VentaId, @ProductoId, @Cantidad, @PrecioUnitario, @Subtotal)";
                    using var cmdDetalle = new SQLiteCommand(queryDetalle, connection, transaction);
                    cmdDetalle.Parameters.AddWithValue("@VentaId", ventaId);
                    cmdDetalle.Parameters.AddWithValue("@ProductoId", detalle.ProductoId);
                    cmdDetalle.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                    cmdDetalle.Parameters.AddWithValue("@PrecioUnitario", detalle.PrecioUnitario);
                    cmdDetalle.Parameters.AddWithValue("@Subtotal", detalle.Subtotal);
                    cmdDetalle.ExecuteNonQuery();

                    // Descontar stock
                    string queryStock = @"UPDATE Productos SET Stock = Stock - @Cantidad WHERE Id = @ProductoId";
                    using var cmdStock = new SQLiteCommand(queryStock, connection, transaction);
                    cmdStock.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                    cmdStock.Parameters.AddWithValue("@ProductoId", detalle.ProductoId);
                    cmdStock.ExecuteNonQuery();
                }

                transaction.Commit();
                return ventaId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void ActualizarVenta(Venta venta)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"UPDATE Ventas SET 
                        UsuarioId = @UsuarioId,
                        Fecha = @Fecha,
                        Total = @Total,
                        Estado = @Estado,
                        MotivoAnulacion = @MotivoAnulacion
                     WHERE Id = @Id";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UsuarioId", venta.UsuarioId);
            cmd.Parameters.AddWithValue("@Fecha", venta.Fecha);
            cmd.Parameters.AddWithValue("@Total", venta.Total);
            cmd.Parameters.AddWithValue("@Estado", venta.Estado ?? "Activa");
            cmd.Parameters.AddWithValue("@MotivoAnulacion", venta.MotivoAnulacion ?? "");
            cmd.Parameters.AddWithValue("@Id", venta.Id);
            cmd.ExecuteNonQuery();
        }

        public List<DetalleVenta> ObtenerDetallesPorVenta(int ventaId)
        {
            var detalles = new List<DetalleVenta>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            string query = @"
                SELECT d.Id, d.VentaId, d.ProductoId, d.Cantidad, d.PrecioUnitario, d.Subtotal,
                       p.Nombre as ProductoNombre
                FROM DetalleVentas d
                JOIN Productos p ON d.ProductoId = p.Id
                WHERE d.VentaId = @VentaId";
            using var cmd = new SQLiteCommand(query, connection);
            cmd.Parameters.AddWithValue("@VentaId", ventaId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                detalles.Add(new DetalleVenta
                {
                    Id = reader.GetInt32(0),
                    VentaId = reader.GetInt32(1),
                    ProductoId = reader.GetInt32(2),
                    Cantidad = reader.GetInt32(3),
                    PrecioUnitario = reader.GetDecimal(4),
                    Subtotal = reader.GetDecimal(5),
                    ProductoNombre = reader.GetString(6)
                });
            }
            return detalles;
        }

        /// <summary>
        /// Anula la venta y devuelve el stock de los productos.
        /// </summary>
        public void AnularVenta(int ventaId, string motivo)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Cambiar estado y motivo
                string query = @"UPDATE Ventas SET Estado = 'Anulada', MotivoAnulacion = @Motivo WHERE Id = @Id";
                using var cmd = new SQLiteCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("@Motivo", motivo);
                cmd.Parameters.AddWithValue("@Id", ventaId);
                cmd.ExecuteNonQuery();

                // Obtener detalles de la venta
                string queryDetalles = "SELECT ProductoId, Cantidad FROM DetalleVentas WHERE VentaId = @VentaId";
                using var cmdDetalles = new SQLiteCommand(queryDetalles, connection, transaction);
                cmdDetalles.Parameters.AddWithValue("@VentaId", ventaId);
                using var reader = cmdDetalles.ExecuteReader();
                var detalles = new List<(int ProductoId, int Cantidad)>();
                while (reader.Read())
                {
                    detalles.Add((reader.GetInt32(0), reader.GetInt32(1)));
                }

                // Revertir stock
                foreach (var d in detalles)
                {
                    string queryStock = @"UPDATE Productos SET Stock = Stock + @Cantidad WHERE Id = @ProductoId";
                    using var cmdStock = new SQLiteCommand(queryStock, connection, transaction);
                    cmdStock.Parameters.AddWithValue("@Cantidad", d.Cantidad);
                    cmdStock.Parameters.AddWithValue("@ProductoId", d.ProductoId);
                    cmdStock.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}