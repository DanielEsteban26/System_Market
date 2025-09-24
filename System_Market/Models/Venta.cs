using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa una venta realizada en el sistema.
    // Incluye información sobre el usuario que la realizó, la fecha, el total y el estado de la venta.
    public class Venta
    {
        // Identificador único de la venta (clave primaria en la base de datos).
        public int Id { get; set; }

        // Id del usuario que realizó la venta (clave foránea a la tabla de usuarios).
        public int UsuarioId { get; set; }

        // Fecha y hora en que se registró la venta.
        public DateTime Fecha { get; set; }

        // Monto total de la venta (suma de todos los productos vendidos).
        public decimal Total { get; set; }

        // Estado de la venta: puede ser "Activa" o "Anulada".
        public string Estado { get; set; } = string.Empty;

        // Motivo de anulación de la venta (solo se usa si la venta fue anulada).
        public string? MotivoAnulacion { get; set; }

        // Nombre del usuario que realizó la venta (usado para mostrar en la interfaz, no se guarda en la base de datos).
        public string UsuarioNombre { get; set; } = string.Empty;
    }
}