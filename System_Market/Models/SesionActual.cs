using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Market.Models
{
    // Clase estática que mantiene la información de la sesión de usuario actualmente activa en la aplicación.
    // Permite acceder al usuario logueado y consultar su rol desde cualquier parte del sistema.
    public static class SesionActual
    {
        // Usuario actualmente autenticado en la aplicación.
        // Es nullable para evitar errores antes de que alguien inicie sesión.
        public static Usuario? Usuario { get; set; }

        // Devuelve true si el usuario actual tiene el rol "Administrador" (ignorando mayúsculas/minúsculas).
        // Útil para controlar permisos y acceso a funcionalidades restringidas.
        public static bool EsAdministrador()
            => Usuario?.Rol != null && string.Equals(Usuario.Rol, "Administrador", StringComparison.OrdinalIgnoreCase);

        // Devuelve true si el usuario actual tiene el rol "Cajero" (ignorando mayúsculas/minúsculas).
        // Permite identificar si el usuario es un cajero y ajustar la interfaz o permisos.
        public static bool EsCajero()
            => Usuario?.Rol != null && string.Equals(Usuario.Rol, "Cajero", StringComparison.OrdinalIgnoreCase);
    }
}