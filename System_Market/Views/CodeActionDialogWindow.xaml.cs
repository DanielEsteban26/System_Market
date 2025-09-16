using System.Windows;

namespace System_Market.Views
{
    public partial class CodeActionDialogWindow : Window
    {
        public enum OpcionSeleccionadaEnum { Ninguna, Venta, Compra, Crear }
        public OpcionSeleccionadaEnum OpcionSeleccionada { get; private set; } = OpcionSeleccionadaEnum.Ninguna;

        public CodeActionDialogWindow(string codigo, bool existeProducto, string rol)
        {
            InitializeComponent();

            // Por defecto ocultar todos
            btnVenta.Visibility = Visibility.Collapsed;
            btnCompra.Visibility = Visibility.Collapsed;
            btnCrear.Visibility = Visibility.Collapsed;

            bool esAdmin = string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase);

            if (esAdmin)
            {
                btnVenta.Visibility = Visibility.Visible;
                btnCompra.Visibility = Visibility.Visible;
                if (!existeProducto)
                    btnCrear.Visibility = Visibility.Visible;
            }
            else // Cajero
            {
                btnVenta.Visibility = Visibility.Visible;
                if (!existeProducto)
                    btnCrear.Visibility = Visibility.Visible;
                // btnCompra no se muestra para cajero
            }
        }

        private void BtnVenta_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Venta;
            DialogResult = true;
        }

        private void BtnCompra_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Compra;
            DialogResult = true;
        }

        private void BtnCrear_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Crear;
            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Ninguna;
            DialogResult = false;
        }
    }
}