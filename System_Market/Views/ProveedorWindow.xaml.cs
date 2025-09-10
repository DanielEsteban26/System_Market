using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    /// <summary>
    /// Lógica de interacción para ProveedorWindow.xaml
    /// </summary>
    public partial class ProveedorWindow : Window
    {
        private readonly ProveedorService _proveedorService;
        private Proveedor proveedorSeleccionado;

        public ProveedorWindow()
        {
            InitializeComponent();
            _proveedorService = new ProveedorService(DatabaseInitializer.GetConnectionString());
            CargarProveedores();
        }

        private void CargarProveedores()
        {
            dgProveedores.ItemsSource = null;
            List<Proveedor> lista = _proveedorService.ObtenerTodos();
            dgProveedores.ItemsSource = lista;
        }

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.");
                return;
            }

            var proveedor = new Proveedor
            {
                Nombre = txtNombre.Text,
                RUC = txtRUC.Text,
                Telefono = txtTelefono.Text
            };

            _proveedorService.AgregarProveedor(proveedor);
            CargarProveedores();
            LimpiarCampos();
        }

        private void btnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor seleccionado)
            {
                seleccionado.Nombre = txtNombre.Text;
                seleccionado.RUC = txtRUC.Text;
                seleccionado.Telefono = txtTelefono.Text;

                _proveedorService.ActualizarProveedor(seleccionado);
                CargarProveedores();
                LimpiarCampos();
            }
            else
            {
                MessageBox.Show("Seleccione un proveedor para actualizar.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor seleccionado)
            {
                _proveedorService.EliminarProveedor(seleccionado.Id);
                CargarProveedores();
                LimpiarCampos();
            }
            else
            {
                MessageBox.Show("Seleccione un proveedor para eliminar.");
            }
        }

        private void btnRefrescar_Click(object sender, RoutedEventArgs e)
        {
            CargarProveedores();
        }

        private void dgProveedores_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor seleccionado)
            {
                proveedorSeleccionado = seleccionado;
                txtNombre.Text = seleccionado.Nombre;
                txtRUC.Text = seleccionado.RUC;
                txtTelefono.Text = seleccionado.Telefono;
            }
        }

        private void LimpiarCampos()
        {
            txtNombre.Text = "";
            txtRUC.Text = "";
            txtTelefono.Text = "";
        }
    }
}
