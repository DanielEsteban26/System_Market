using System;
using System.Data.SQLite;
using System.IO;

namespace System_Market.Data
{
    // Clase encargada de inicializar la base de datos SQLite del sistema.
    // Se asegura de que la carpeta, el archivo y todas las tablas necesarias existan.
    public static class DatabaseInitializer
    {
        // Ruta de la carpeta donde se guardará la base de datos (en AppData\Roaming\Minimarket)
        private static string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minimarket");

        // Nombre del archivo de la base de datos
        private static string dbFile = "minimarket.db";
        // Ruta completa al archivo de la base de datos
        private static string dbPath = Path.Combine(folderPath, dbFile);
        // Cadena de conexión para acceder a la base de datos SQLite
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        // Inicializa la base de datos: crea la carpeta, el archivo y las tablas si no existen.
        public static void InitializeDatabase()
        {
            // Si la carpeta donde irá la base de datos no existe, la crea.
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("📂 Carpeta creada en: " + folderPath);
            }

            // Si el archivo de la base de datos no existe, lo crea.
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                Console.WriteLine("📦 Base de datos creada en: " + dbPath);
            }

            // Abre la conexión a la base de datos para crear/verificar las tablas.
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                // Script para crear la tabla de usuarios, con roles y validación de rol.
                string sqlUsuarios = @"
                CREATE TABLE IF NOT EXISTS Usuarios (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL,
                    Usuario TEXT NOT NULL UNIQUE,
                    Clave TEXT NOT NULL,
                    Rol TEXT NOT NULL CHECK(Rol IN ('Administrador', 'Cajero'))
                );";

                // Script para crear la tabla de proveedores.
                string sqlProveedores = @"
                CREATE TABLE IF NOT EXISTS Proveedores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL,
                    RUC TEXT,
                    Telefono TEXT
                );";

                // Script para crear la tabla de categorías de productos.
                string sqlCategorias = @"
                CREATE TABLE IF NOT EXISTS Categorias (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL
                );";

                // Script para crear la tabla de productos, con claves foráneas a categorías y proveedores.
                string sqlProductos = @"
                CREATE TABLE IF NOT EXISTS Productos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CodigoBarras TEXT UNIQUE,
                    Nombre TEXT NOT NULL,
                    CategoriaId INTEGER,
                    ProveedorId INTEGER,
                    PrecioCompra REAL NOT NULL,
                    PrecioVenta REAL NOT NULL,
                    Stock INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CategoriaId) REFERENCES Categorias(Id),
                    FOREIGN KEY (ProveedorId) REFERENCES Proveedores(Id)
                );";

                // Script para crear la tabla de compras.
                string sqlCompras = @"
                CREATE TABLE IF NOT EXISTS Compras (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId INTEGER NOT NULL,
                    ProveedorId INTEGER NOT NULL,
                    Fecha DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Total REAL NOT NULL,
                    Estado TEXT NOT NULL DEFAULT 'Activa' CHECK(Estado IN ('Activa','Anulada')),
                    MotivoAnulacion TEXT,
                    FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id),
                    FOREIGN KEY (ProveedorId) REFERENCES Proveedores(Id)
                );";

                // Script para crear la tabla de detalle de compras (productos comprados en cada compra).
                string sqlDetalleCompras = @"
                CREATE TABLE IF NOT EXISTS DetalleCompras (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompraId INTEGER NOT NULL,
                    ProductoId INTEGER NOT NULL,
                    Cantidad INTEGER NOT NULL,
                    PrecioUnitario REAL NOT NULL,
                    Subtotal REAL NOT NULL,
                    FOREIGN KEY (CompraId) REFERENCES Compras(Id),
                    FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
                );";

                // Script para crear la tabla de ventas.
                string sqlVentas = @"
                CREATE TABLE IF NOT EXISTS Ventas (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId INTEGER NOT NULL,
                    Fecha DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Total REAL NOT NULL,
                    Estado TEXT NOT NULL DEFAULT 'Activa' CHECK(Estado IN ('Activa','Anulada')),
                    MotivoAnulacion TEXT,
                    FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
                );";

                // Script para crear la tabla de detalle de ventas (productos vendidos en cada venta).
                string sqlDetalleVentas = @"
                CREATE TABLE IF NOT EXISTS DetalleVentas (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    VentaId INTEGER NOT NULL,
                    ProductoId INTEGER NOT NULL,
                    Cantidad INTEGER NOT NULL,
                    PrecioUnitario REAL NOT NULL,
                    Subtotal REAL NOT NULL,
                    FOREIGN KEY (VentaId) REFERENCES Ventas(Id),
                    FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
                );";

                // Ejecuta cada script SQL para crear/verificar las tablas en la base de datos.
                var cmd = new SQLiteCommand(sqlUsuarios, connection);
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlProveedores;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlCategorias;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlProductos;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlCompras;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlDetalleCompras;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlVentas;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlDetalleVentas;
                cmd.ExecuteNonQuery();

                Console.WriteLine("✅ Tablas creadas o verificadas correctamente.");
            }
        }

        // Devuelve la cadena de conexión a la base de datos SQLite para ser usada en el resto del sistema.
        public static string GetConnectionString() => connectionString;
    }
}