using System.Windows;

namespace System_Market.Views
{
    // Ventana modal para solicitar al usuario el motivo de anulación de una compra o venta.
    public partial class MotivoAnulacionWindow : Window
    {
        // Propiedad donde se almacena el motivo ingresado por el usuario.
        public string Motivo { get; private set; }

        // Constructor: inicializa los componentes de la ventana.
        public MotivoAnulacionWindow()
        {
            InitializeComponent();
        }

        // Evento: se ejecuta al hacer clic en el botón Confirmar.
        // Valida que el motivo no esté vacío y, si es válido, lo guarda y cierra la ventana con resultado positivo.
        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMotivo.Text))
            {
                MessageBox.Show("Debe ingresar un motivo.");
                return;
            }
            Motivo = txtMotivo.Text.Trim();
            DialogResult = true;
        }
    }
}