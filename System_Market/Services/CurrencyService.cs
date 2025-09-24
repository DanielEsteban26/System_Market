using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace System_Market.Services
{
    // Servicio estático para formatear y parsear valores monetarios en soles peruanos,
    // adaptándose a distintas culturas y formatos de entrada.
    public static class CurrencyService
    {
        // Formatea un valor decimal como moneda en soles ("S/ "), usando la cultura actual del hilo.
        // Ejemplo de salida: "S/ 1,234.56" o "S/ 1.234,56" según la configuración regional.
        public static string FormatSoles(decimal amount, string format = "N2")
        {
            var ci = Thread.CurrentThread.CurrentCulture;
            return "S/ " + amount.ToString(format, ci);
        }

        // Sobrecarga para valores double, convierte a decimal antes de formatear.
        public static string FormatSoles(double amount, string format = "N2") =>
            FormatSoles(Convert.ToDecimal(amount), format);

        // Formatea solo el número (sin símbolo "S/"), útil para campos de entrada o edición.
        public static string FormatNumber(decimal amount, string format = "N2")
        {
            var ci = Thread.CurrentThread.CurrentCulture;
            return amount.ToString(format, ci);
        }

        // Sobrecarga para valores double, convierte a decimal antes de formatear.
        public static string FormatNumber(double amount, string format = "N2") =>
            FormatNumber(Convert.ToDecimal(amount), format);

        // Intenta convertir un texto a decimal, aceptando variantes con "S/", separadores de miles y decimales
        // en diferentes culturas (por ejemplo: "S/ 1,234.56", "S/1.234,56", "1234,56", etc.).
        // Devuelve true si el parseo fue exitoso y el valor en 'valor'.
        public static bool TryParseSoles(string? texto, out decimal valor)
        {
            valor = 0m;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            // Elimina el símbolo de soles y espacios no separables (NBSP), dejando solo el número.
            texto = texto.Replace("S/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("s/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("\u00A0", "") // NBSP
                         .Trim();

            // Intenta parsear usando varias culturas: la actual, la de Perú y la invariante.
            var cultures = new[] { CultureInfo.CurrentCulture, new CultureInfo("es-PE"), CultureInfo.InvariantCulture };
            var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowThousands;

            foreach (var c in cultures)
            {
                if (decimal.TryParse(texto, styles, c, out valor))
                    return true;
            }

            // Si falla, normaliza el texto: deja solo dígitos, puntos, comas y signos.
            var sb = new StringBuilder();
            foreach (var ch in texto)
            {
                if (char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-' || ch == '+')
                    sb.Append(ch);
            }
            var raw = sb.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // Si hay punto y coma, asume que el separador decimal es el que está más a la derecha.
            if (raw.Contains('.') && raw.Contains(','))
            {
                int lastDot = raw.LastIndexOf('.');
                int lastComma = raw.LastIndexOf(',');

                char decimalSep = lastDot > lastComma ? '.' : ',';
                char thousandsSep = decimalSep == '.' ? ',' : '.';

                var cleaned = raw.Replace(thousandsSep.ToString(), "");
                cleaned = cleaned.Replace(decimalSep, '.');

                if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                    return true;

                return false;
            }

            // Si solo hay un separador (punto o coma), decide si es decimal o de miles según la posición.
            if (raw.Contains('.') || raw.Contains(','))
            {
                char sep = raw.Contains('.') ? '.' : ',';
                int idx = raw.LastIndexOf(sep);
                int digitsAfter = raw.Length - idx - 1;

                // Si hay 1 o 2 dígitos a la derecha, probablemente es decimal.
                if (digitsAfter > 0 && digitsAfter <= 2)
                {
                    var normalized = raw.Replace(sep, '.');
                    if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                        return true;
                }
                else
                {
                    // Si no, probablemente es separador de miles, así que lo elimina.
                    var cleaned = raw.Replace(sep.ToString(), "");
                    if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                        return true;
                }
            }
            else
            {
                // Si no hay separadores, intenta parsear directamente.
                if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                    return true;
            }

            // Último intento: reemplaza comas por puntos y parsea como invariante.
            var fallback = raw.Replace(',', '.');
            return decimal.TryParse(fallback, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor);
        }
    }
}