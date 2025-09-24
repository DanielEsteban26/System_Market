using System;
using System.IO;

namespace System_Market.Services
{
    // Servicio estático para contar cuántas veces se ha ejecutado la aplicación.
    // Guarda el contador en un archivo de texto dentro de AppData\Roaming\Minimarket.
    public static class ExecutionCounterService
    {
        // Ruta de la carpeta donde se almacena el archivo de contador.
        private static string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minimarket");

        // Ruta completa al archivo que almacena el número de ejecuciones.
        private static string counterFile = Path.Combine(folderPath, "execution_count.txt");

        // Obtiene el número actual de ejecuciones de la aplicación.
        // Si el archivo no existe, retorna 0.
        public static int GetExecutionCount()
        {
            if (!File.Exists(counterFile))
                return 0;
            var text = File.ReadAllText(counterFile);
            // Intenta convertir el texto leído a entero, si falla retorna 0.
            return int.TryParse(text, out int count) ? count : 0;
        }

        // Incrementa el contador de ejecuciones y lo guarda en el archivo.
        public static void IncrementExecutionCount()
        {
            int count = GetExecutionCount() + 1;
            // Guarda el nuevo valor en el archivo (lo crea si no existe).
            File.WriteAllText(counterFile, count.ToString());
        }
    }
}