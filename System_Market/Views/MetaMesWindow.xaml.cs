using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace System_Market.Views
{
    public partial class MetaMesWindow : Window
    {
        public decimal? MetaDefinida { get; private set; }

        public MetaMesWindow(decimal? metaActual)
        {
            InitializeComponent();
            if (metaActual.HasValue)
                txtMeta.Text = metaActual.Value.ToString("N2");
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtMeta.Text.Replace(",", ""), out var meta) && meta > 0)
            {
                MetaDefinida = meta;
                DialogResult = true;
            }
            else
                MessageBox.Show("Ingrese un monto válido (> 0)", "Meta", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void NumeroDecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permite dígitos y un único separador . o ,
            e.Handled = !Regex.IsMatch(e.Text, @"[\d\.,]");
        }

        private void TxtMeta_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            btnAceptar.IsEnabled = decimal.TryParse(
                txtMeta.Text.Replace(",", "."),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var val) && val > 0;
        }
    }
}