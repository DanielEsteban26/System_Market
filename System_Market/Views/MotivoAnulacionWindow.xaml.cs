using System.Windows;

namespace System_Market.Views
{
    public partial class MotivoAnulacionWindow : Window
    {
        public string Motivo { get; private set; }

        public MotivoAnulacionWindow()
        {
            InitializeComponent();
        }

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