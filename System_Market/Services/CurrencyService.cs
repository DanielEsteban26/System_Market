using System;
using System.Globalization;
using System.Threading;

namespace System_Market.Services
{
    public static class CurrencyService
    {
        // Formatea con símbolo "S/ " y la cultura actual del hilo (ej. "S/ 1.234,56")
        public static string FormatSoles(decimal amount, string format = "N2")
        {
            var ci = Thread.CurrentThread.CurrentCulture;
            return "S/ " + amount.ToString(format, ci);
        }

        public static string FormatSoles(double amount, string format = "N2") =>
            FormatSoles(Convert.ToDecimal(amount), format);

        // Formatea sólo el número (sin símbolo), útil para campos de entrada
        public static string FormatNumber(decimal amount, string format = "N2")
        {
            var ci = Thread.CurrentThread.CurrentCulture;
            return amount.ToString(format, ci);
        }

        public static string FormatNumber(double amount, string format = "N2") =>
            FormatNumber(Convert.ToDecimal(amount), format);
    }
}