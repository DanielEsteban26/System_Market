using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa un proveedor dentro del sistema.
    // Un proveedor es una entidad o empresa que suministra productos al minimarket.
    public class Proveedor
    {
        // Identificador único del proveedor (clave primaria en la base de datos).
        public int Id { get; set; }

        // Nombre del proveedor o razón social.
        public string Nombre { get; set; } = string.Empty;

        // RUC (Registro Único de Contribuyentes) del proveedor, usado para identificación fiscal.
        public string RUC { get; set; } = string.Empty;

        // Teléfono de contacto del proveedor.
        public string Telefono { get; set; } = string.Empty;
    }
}