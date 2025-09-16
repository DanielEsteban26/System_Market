using System;
using System.IO;

namespace System_Market.Services
{
    public static class ExecutionCounterService
    {
        private static string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minimarket");
        private static string counterFile = Path.Combine(folderPath, "execution_count.txt");

        public static int GetExecutionCount()
        {
            if (!File.Exists(counterFile))
                return 0;
            var text = File.ReadAllText(counterFile);
            return int.TryParse(text, out int count) ? count : 0;
        }

        public static void IncrementExecutionCount()
        {
            int count = GetExecutionCount() + 1;
            File.WriteAllText(counterFile, count.ToString());
        }
    }
}