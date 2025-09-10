using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    public class Compra
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int ProveedorId { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty; // Activa / Anulada
        public string MotivoAnulacion { get; set; } = string.Empty;

        // Auxiliares para UI
        public string UsuarioNombre { get; set; } = string.Empty;
        public string ProveedorNombre { get; set; } = string.Empty;
    }
}
