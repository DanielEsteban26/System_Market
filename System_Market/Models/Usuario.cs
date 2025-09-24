using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Representa un usuario del sistema, ya sea administrador o cajero.
    // Contiene la información necesaria para autenticación y control de permisos.
    public class Usuario
    {
        // Identificador único del usuario (clave primaria en la base de datos).
        public int Id { get; set; }

        // Nombre completo del usuario (por ejemplo: "Juan Pérez").
        public string Nombre { get; set; } = string.Empty;

        // Nombre de usuario utilizado para iniciar sesión (por ejemplo: "jperez").
        public string UsuarioNombre { get; set; } = string.Empty;

        // Contraseña del usuario (almacenada como texto, se recomienda cifrar en producción).
        public string Clave { get; set; } = string.Empty;

        // Rol asignado al usuario, determina los permisos (por ejemplo: "Administrador" o "Cajero").
        public string Rol { get; set; } = string.Empty;
    }
}