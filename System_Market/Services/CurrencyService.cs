using System;
using System.Globalization;
using System.Linq;
using System.Text;
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

        // Intenta parsear textos que pueden incluir "S/" o separadores de miles/decimal en distintas culturas.
        public static bool TryParseSoles(string? texto, out decimal valor)
        {
            valor = 0m;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            // Quitar símbolo de soles y espacios comunes
            texto = texto.Replace("S/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("s/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("\u00A0", "") // NBSP
                         .Trim();

            // Primera aproximación: intentar con culturas (actual, es-PE, invariant)
            var cultures = new[] { CultureInfo.CurrentCulture, new CultureInfo("es-PE"), CultureInfo.InvariantCulture };
            var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowThousands;

            foreach (var c in cultures)
            {
                if (decimal.TryParse(texto, styles, c, out valor))
                    return true;
            }

            // Normalización robusta: decidir separador decimal eliminando separadores de miles
            // Limpiar caracteres que no sean dígitos, '.' ',' o signo '-' (mantener lo esencial)
            var sb = new StringBuilder();
            foreach (var ch in texto)
            {
                if (char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-' || ch == '+')
                    sb.Append(ch);
            }
            var raw = sb.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // Si contiene ambos, asumir que el separador decimal es el que está más a la derecha
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

            // Si solo hay uno de los separadores:
            if (raw.Contains('.') || raw.Contains(','))
            {
                char sep = raw.Contains('.') ? '.' : ',';
                int idx = raw.LastIndexOf(sep);
                int digitsAfter = raw.Length - idx - 1;

                // Si hay 1 o 2 dígitos a la derecha -> probablemente es separador decimal
                if (digitsAfter > 0 && digitsAfter <= 2)
                {
                    var normalized = raw.Replace(sep, '.');
                    if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                        return true;
                }
                else
                {
                    // Probablemente es separador de miles -> eliminarlo
                    var cleaned = raw.Replace(sep.ToString(), "");
                    if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                        return true;
                }
            }
            else
            {
                // No separadores: intentar parse directo invariant
                if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                    return true;
            }

            // Último intento: reemplazar coma por punto y parsear invariant
            var fallback = raw.Replace(',', '.');
            return decimal.TryParse(fallback, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor);
        }
    }
}