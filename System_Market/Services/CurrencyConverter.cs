using System;
using System.Globalization;
using System.Windows.Data;

namespace System_Market.Services
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            string fmt = parameter as string ?? "N2";
            if (value is decimal dec) return CurrencyService.FormatSoles(dec, fmt);
            if (value is double d) return CurrencyService.FormatSoles(d, fmt);
            if (decimal.TryParse(value.ToString(), out var parsed)) return CurrencyService.FormatSoles(parsed, fmt);
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}