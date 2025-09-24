using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa una compra realizada en el sistema.
    // Incluye información sobre el usuario que realizó la compra, el proveedor, la fecha, el total y el estado.
    public class Compra
    {
        // Identificador único de la compra (clave primaria en la base de datos).
        public int Id { get; set; }

        // Id del usuario que realizó la compra (clave foránea a la tabla de usuarios).
        public int UsuarioId { get; set; }

        // Id del proveedor al que se le realizó la compra (clave foránea a la tabla de proveedores).
        public int ProveedorId { get; set; }

        // Fecha y hora en que se registró la compra.
        public DateTime Fecha { get; set; }

        // Monto total de la compra (suma de todos los productos y cantidades).
        public decimal Total { get; set; }

        // Estado de la compra: puede ser "Activa" o "Anulada".
        public string Estado { get; set; } = string.Empty;

        // Si la compra fue anulada, aquí se almacena el motivo de la anulación.
        public string MotivoAnulacion { get; set; } = string.Empty;

        // Nombre del usuario que realizó la compra (usado para mostrar en la interfaz, no se guarda en la base de datos).
        public string UsuarioNombre { get; set; } = string.Empty;

        // Nombre del proveedor de la compra (usado para mostrar en la interfaz, no se guarda en la base de datos).
        public string ProveedorNombre { get; set; } = string.Empty;
    }
}