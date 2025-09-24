namespace System_Market.Models
{
    // Representa una categoría de productos dentro del sistema.
    // Se utiliza para clasificar y organizar los productos en el inventario.
    public class Categoria
    {
        // Identificador único de la categoría (clave primaria en la base de datos).
        public int Id { get; set; }

        // Nombre descriptivo de la categoría (por ejemplo: "Bebidas", "Lácteos", etc.).
        public string Nombre { get; set; } = string.Empty;
    }
}