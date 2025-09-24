using System;
using System.Globalization;
using System.Windows.Data;

namespace System_Market.Services
{
    // Convertidor de valores para mostrar montos en soles peruanos formateados en la interfaz WPF.
    // Se utiliza en bindings de XAML para mostrar valores numéricos como moneda local.
    public class CurrencyConverter : IValueConverter
    {
        // Convierte un valor numérico a un string formateado como moneda (S/ 0.00).
        // El parámetro opcional permite especificar el formato numérico (por defecto "N2" = dos decimales).
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si el valor es nulo, retorna cadena vacía.
            if (value == null) return string.Empty;

            // Obtiene el formato a usar, por defecto "N2" (dos decimales).
            string fmt = parameter as string ?? "N2";

            // Si el valor es decimal, lo formatea usando el servicio de moneda.
            if (value is decimal dec) return CurrencyService.FormatSoles(dec, fmt);

            // Si el valor es double, también lo formatea.
            if (value is double d) return CurrencyService.FormatSoles(d, fmt);

            // Si el valor es convertible a decimal, lo intenta formatear.
            if (decimal.TryParse(value.ToString(), out var parsed)) return CurrencyService.FormatSoles(parsed, fmt);

            // Si no es un valor numérico, lo retorna tal cual.
            return value.ToString();
        }

        // No se soporta la conversión inversa (de string a valor numérico).
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}