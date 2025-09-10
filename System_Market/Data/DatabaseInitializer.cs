using System;
using System.Data.SQLite;
using System.IO;

namespace System_Market.Data
{
    public static class DatabaseInitializer
    {
        // Carpeta en AppData\Roaming\Minimarket
        private static string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minimarket");

        private static string dbFile = "minimarket.db";
        private static string dbPath = Path.Combine(folderPath, dbFile);
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        public static void InitializeDatabase()
        {
            // Crear carpeta en AppData si no existe
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("📂 Carpeta creada en: " + folderPath);
            }

            // Crear archivo si no existe
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                Console.WriteLine("📦 Base de datos creada en: " + dbPath);
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string sqlUsuarios = @"
                CREATE TABLE IF NOT EXISTS Usuarios (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL,
                    Usuario TEXT NOT NULL UNIQUE,
                    Clave TEXT NOT NULL,
                    Rol TEXT NOT NULL CHECK(Rol IN ('Administrador', 'Cajero'))
                );";

                string sqlProveedores = @"
                CREATE TABLE IF NOT EXISTS Proveedores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL,
                    RUC TEXT,
                    Telefono TEXT
                );";

                string sqlCategorias = @"
                CREATE TABLE IF NOT EXISTS Categorias (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL
                );";

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

                // Ejecutar todas las sentencias
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

        public static string GetConnectionString() => connectionString;
    }
}
