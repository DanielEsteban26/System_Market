using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa un producto dentro del sistema de inventario y ventas.
    // Incluye información básica, relaciones con categoría y proveedor, y datos auxiliares para la interfaz.
    public class Producto
    {
        // Identificador único del producto (clave primaria en la base de datos).
        public int Id { get; set; }

        // Código de barras del producto (puede ser nulo si el producto no tiene código asignado).
        public string? CodigoBarras { get; set; }

        // Nombre descriptivo del producto.
        public string Nombre { get; set; } = string.Empty;

        // Id de la categoría a la que pertenece el producto (puede ser nulo si no está categorizado).
        public int? CategoriaId { get; set; }

        // Id del proveedor asociado al producto (puede ser nulo si no tiene proveedor asignado).
        public int? ProveedorId { get; set; }

        // Precio de compra del producto (lo que costó adquirirlo).
        public decimal PrecioCompra { get; set; }

        // Precio de venta del producto (lo que se cobra al cliente).
        public decimal PrecioVenta { get; set; }

        // Cantidad disponible en inventario.
        public int Stock { get; set; }

        // Nombre de la categoría (usado solo para mostrar en la interfaz, no se guarda en la base de datos).
        public string CategoriaNombre { get; set; } = string.Empty;

        // Nombre del proveedor (usado solo para mostrar en la interfaz, no se guarda en la base de datos).
        public string ProveedorNombre { get; set; } = string.Empty;
    }
}