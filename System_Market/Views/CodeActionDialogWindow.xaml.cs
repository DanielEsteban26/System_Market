using System.Windows;

namespace System_Market.Views
{
    public partial class CodeActionDialogWindow : Window
    {
        // Enum para identificar la opción seleccionada por el usuario en el diálogo
        public enum OpcionSeleccionadaEnum { Ninguna, Venta, Compra, Crear }

        // Propiedad que expone la opción elegida al cerrar el diálogo
        public OpcionSeleccionadaEnum OpcionSeleccionada { get; private set; } = OpcionSeleccionadaEnum.Ninguna;

        /// <summary>
        /// Constructor. Configura la visibilidad de los botones según el rol y si el producto existe.
        /// </summary>
        /// <param name="codigo">Código escaneado o ingresado</param>
        /// <param name="existeProducto">Indica si el producto ya existe</param>
        /// <param name="rol">Rol del usuario (Administrador o Cajero)</param>
        public CodeActionDialogWindow(string codigo, bool existeProducto, string rol)
        {
            InitializeComponent();

            // Por defecto, oculta todos los botones de acción
            btnVenta.Visibility = Visibility.Collapsed;
            btnCompra.Visibility = Visibility.Collapsed;
            btnCrear.Visibility = Visibility.Collapsed;

            bool esAdmin = string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase);

            if (esAdmin)
            {
                // El administrador puede vender, comprar y crear producto si no existe
                btnVenta.Visibility = Visibility.Visible;
                btnCompra.Visibility = Visibility.Visible;
                if (!existeProducto)
                    btnCrear.Visibility = Visibility.Visible;
            }
            else // Cajero
            {
                // El cajero solo puede vender y crear producto si no existe
                btnVenta.Visibility = Visibility.Visible;
                if (!existeProducto)
                    btnCrear.Visibility = Visibility.Visible;
                // btnCompra no se muestra para cajero
            }
        }

        // Evento: el usuario selecciona "Venta"
        private void BtnVenta_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Venta;
            DialogResult = true;
        }

        // Evento: el usuario selecciona "Compra"
        private void BtnCompra_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Compra;
            DialogResult = true;
        }

        // Evento: el usuario selecciona "Crear producto"
        private void BtnCrear_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Crear;
            DialogResult = true;
        }

        // Evento: el usuario cancela la acción
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            OpcionSeleccionada = OpcionSeleccionadaEnum.Ninguna;
            DialogResult = false;
        }
    }
}