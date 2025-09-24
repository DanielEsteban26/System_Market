using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa el detalle de un producto incluido en una compra.
    // Cada instancia corresponde a un producto específico comprado en una operación.
    public class DetalleCompra
    {
        // Identificador único del detalle de compra (clave primaria en la base de datos).
        public int Id { get; set; }

        // Id de la compra a la que pertenece este detalle (clave foránea a la tabla de compras).
        public int CompraId { get; set; }

        // Id del producto comprado (clave foránea a la tabla de productos).
        public int ProductoId { get; set; }

        // Cantidad de unidades del producto compradas en esta operación.
        public int Cantidad { get; set; }

        // Precio unitario del producto en el momento de la compra.
        public decimal PrecioUnitario { get; set; }

        // Subtotal de este producto en la compra (Cantidad * PrecioUnitario).
        public decimal Subtotal { get; set; }

        // Nombre del producto (usado para mostrar en la interfaz, no se guarda en la base de datos).
        public string ProductoNombre { get; set; } = string.Empty;
    }
}