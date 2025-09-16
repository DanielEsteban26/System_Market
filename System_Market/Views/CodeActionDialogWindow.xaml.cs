using System.Windows;

namespace System_Market.Views
{
    public enum CodeActionResult
    {
        None,
        Venta,
        Compra
    }

    public partial class CodeActionDialogWindow : Window
    {
        public CodeActionResult Resultado { get; private set; } = CodeActionResult.None;
        private readonly string _codigo;

        public CodeActionDialogWindow(string codigo)
        {
            InitializeComponent();
            _codigo = codigo;
            txtCodigo.Text = $"Código: {codigo}";
        }

        private void BtnVenta_Click(object sender, RoutedEventArgs e)
        {
            Resultado = CodeActionResult.Venta;
            DialogResult = true;
        }

        private void BtnCompra_Click(object sender, RoutedEventArgs e)
        {
            Resultado = CodeActionResult.Compra;
            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}