using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    public static class SesionActual
    {
        // Nullable para evitar problemas antes del login
        public static Usuario? Usuario { get; set; }

        public static bool EsAdministrador()
            => Usuario?.Rol != null && string.Equals(Usuario.Rol, "Administrador", StringComparison.OrdinalIgnoreCase);

        public static bool EsCajero()
            => Usuario?.Rol != null && string.Equals(Usuario.Rol, "Cajero", StringComparison.OrdinalIgnoreCase);
    }
}
