using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace System_Market.Behaviors
{
    /// <summary>
    /// Comportamiento adjunto para permitir solo la entrada de decimales en un TextBox.
    /// Se puede activar con: DecimalInput.Enable="True"
    /// </summary>
    public static class DecimalInput
    {
        // Permite obtener si el comportamiento está habilitado en el control.
        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

        // Permite establecer si el comportamiento está habilitado en el control.
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        // Propiedad adjunta para activar/desactivar el comportamiento en un TextBox.
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable", // Nombre de la propiedad
                typeof(bool), // Tipo de la propiedad
                typeof(DecimalInput), // Clase propietaria
                new PropertyMetadata(false, OnEnableChanged) // Valor por defecto y callback de cambio
            );

        // Se ejecuta cuando cambia el valor de la propiedad Enable en el TextBox.
        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;

            // Si se habilita, se suscriben los eventos para controlar la entrada.
            if ((bool)e.NewValue)
            {
                tb.PreviewTextInput += Tb_PreviewTextInput; // Valida cada carácter ingresado
                tb.PreviewKeyDown += Tb_PreviewKeyDown;     // Bloquea la barra espaciadora
                DataObject.AddPastingHandler(tb, OnPaste);  // Valida el texto pegado
                tb.LostFocus += Tb_LostFocus;               // Formatea el valor al perder el foco
            }
            else // Si se deshabilita, se quitan los eventos.
            {
                tb.PreviewTextInput -= Tb_PreviewTextInput;
                tb.PreviewKeyDown -= Tb_PreviewKeyDown;
                DataObject.RemovePastingHandler(tb, OnPaste);
                tb.LostFocus -= Tb_LostFocus;
            }
        }

        // Expresión regular que permite solo números, puntos y comas.
        private static readonly Regex _allowed = new(@"^[0-9\.,]+$", RegexOptions.Compiled);

        // Evento que valida cada carácter ingresado por teclado.
        private static void Tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Si el carácter no es válido, se bloquea la entrada.
            if (!_allowed.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }
        }

        // Evento que bloquea la barra espaciadora.
        private static void Tb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        // Evento que valida el texto cuando se pega desde el portapapeles.
        private static void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)) return;
            var txt = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!;
            // Si el texto pegado no es válido, se cancela el pegado.
            if (!_allowed.IsMatch(txt))
                e.CancelCommand();
        }

        // Evento que se ejecuta cuando el TextBox pierde el foco.
        // Normaliza y formatea el valor como decimal con dos decimales.
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
                // Muestra el valor con dos decimales usando la cultura actual.
                tb.Text = val.ToString("N2", CultureInfo.CurrentCulture);
            }
            else
            {
                tb.Text = "0.00";
            }
        }

        // Normaliza el texto para que sea parseable como decimal.
        // Reemplaza comas por puntos, elimina caracteres no válidos y deja solo un punto decimal.
        private static string Normalizar(string input)
        {
            input = input.Trim();
            input = input.Replace(',', '.'); // Unifica separador decimal
            input = Regex.Replace(input, @"[^0-9.]", ""); // Elimina todo menos dígitos y puntos

            // Si hay más de un punto, elimina los extras dejando solo el primero.
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