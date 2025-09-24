using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa el detalle de un producto vendido en una venta específica.
    // Cada instancia corresponde a un producto incluido en una operación de venta.
    public class DetalleVenta
    {
        // Identificador único del detalle de venta (clave primaria en la base de datos).
        public int Id { get; set; }

        // Id de la venta a la que pertenece este detalle (clave foránea a la tabla de ventas).
        public int VentaId { get; set; }

        // Id del producto vendido (clave foránea a la tabla de productos).
        public int ProductoId { get; set; }

        // Cantidad de unidades del producto vendidas en esta operación.
        public int Cantidad { get; set; }

        // Precio unitario del producto en el momento de la venta.
        public decimal PrecioUnitario { get; set; }

        // Subtotal de este producto en la venta (Cantidad * PrecioUnitario).
        public decimal Subtotal { get; set; }

        // Nombre del producto (usado para mostrar en la interfaz, no se guarda en la base de datos).
        public string ProductoNombre { get; set; } = string.Empty;
    }
}