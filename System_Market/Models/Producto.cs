using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string? CodigoBarras { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public int? ProveedorId { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public int Stock { get; set; }
        // Propiedades auxiliares para mostrar en la UI
        public string CategoriaNombre { get; set; } = string.Empty;
        public string ProveedorNombre { get; set; } = string.Empty;
    }
}
