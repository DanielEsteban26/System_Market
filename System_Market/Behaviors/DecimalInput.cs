using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace System_Market.Behaviors
{
    public static class DecimalInput
    {
        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(DecimalInput),
                new PropertyMetadata(false, OnEnableChanged));

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;
            if ((bool)e.NewValue)
            {
                tb.PreviewTextInput += Tb_PreviewTextInput;
                tb.PreviewKeyDown += Tb_PreviewKeyDown;
                DataObject.AddPastingHandler(tb, OnPaste);
                tb.LostFocus += Tb_LostFocus;
            }
            else
            {
                tb.PreviewTextInput -= Tb_PreviewTextInput;
                tb.PreviewKeyDown -= Tb_PreviewKeyDown;
                DataObject.RemovePastingHandler(tb, OnPaste);
                tb.LostFocus -= Tb_LostFocus;
            }
        }

        private static readonly Regex _allowed = new(@"^[0-9\.,]+$", RegexOptions.Compiled);
        private static void Tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_allowed.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }
        }

        private static void Tb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private static void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)) return;
            var txt = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!;
            if (!_allowed.IsMatch(txt))
                e.CancelCommand();
        }

        private static void Tb_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = "0.00";
                return;
            }

            var normalized = Normalizar(tb.Text);
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                // Usa la cultura actual para mostrar
                tb.Text = val.ToString("N2", CultureInfo.CurrentCulture);
            }
            else
            {
                tb.Text = "0.00";
            }
        }

        private static string Normalizar(string input)
        {
            input = input.Trim();
            // Reemplazar coma por punto para parse Invariant
            input = input.Replace(',', '.');
            // Quitar todo lo que no sea dígito o punto
            input = Regex.Replace(input, @"[^0-9.]", "");
            // Si hay más de un punto, conservar el primero
            int first = input.IndexOf('.');
            if (first >= 0)
            {
                int second = input.IndexOf('.', first + 1);
                while (second > 0)
                {
                    input = input.Remove(second, 1);
                    second = input.IndexOf('.', first + 1);
                }
            }
            return input;
        }
    }
}