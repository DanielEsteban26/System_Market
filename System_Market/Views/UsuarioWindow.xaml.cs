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
    // Ventana para la gestión de usuarios del sistema (administradores y cajeros).
    // Permite agregar, actualizar, eliminar y listar usuarios.
    public partial class UsuarioWindow : Window
    {
        private readonly UsuarioService _usuarioService;
        private Usuario _usuarioSeleccionado;

        public UsuarioWindow()
        {
            InitializeComponent();
            _usuarioService = new UsuarioService(DatabaseInitializer.GetConnectionString());
            CargarUsuarios();
        }

        // Carga la lista de usuarios y la muestra en el DataGrid
        private void CargarUsuarios()
        {
            dgUsuarios.ItemsSource = null;
            List<Usuario> lista = _usuarioService.ObtenerTodos();
            dgUsuarios.ItemsSource = lista;
        }

        // Agrega un nuevo usuario validando los campos y el rol
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text) ||
                string.IsNullOrWhiteSpace(txtUsuario.Text) ||
                string.IsNullOrWhiteSpace(txtClave.Password) ||
                cbRol.SelectedItem == null)
            {
                MessageBox.Show("Complete todos los campos.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show("¿Desea agregar este usuario?", "Confirmar agregado", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes)
                return;

            var usuario = new Usuario
            {
                Nombre = txtNombre.Text,
                UsuarioNombre = txtUsuario.Text,
                Clave = txtClave.Password,
                Rol = ((ComboBoxItem)cbRol.SelectedItem).Content.ToString()!
            };

            try
            {
                _usuarioService.AgregarUsuario(usuario);
                CargarUsuarios();
                LimpiarCampos();
                MessageBox.Show("Usuario agregado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al agregar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Actualiza el usuario seleccionado con los datos del formulario
        private void btnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioSeleccionado == null)
            {
                MessageBox.Show("Seleccione un usuario para actualizar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show("¿Desea actualizar este usuario?", "Confirmar actualización", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes)
                return;

            _usuarioSeleccionado.Nombre = txtNombre.Text;
            _usuarioSeleccionado.UsuarioNombre = txtUsuario.Text;
            _usuarioSeleccionado.Clave = txtClave.Password;
            _usuarioSeleccionado.Rol = ((ComboBoxItem)cbRol.SelectedItem).Content.ToString()!;

            try
            {
                _usuarioService.ActualizarUsuario(_usuarioSeleccionado);
                CargarUsuarios();
                LimpiarCampos();
                MessageBox.Show("Usuario actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al actualizar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Elimina el usuario seleccionado tras confirmación
        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioSeleccionado == null)
            {
                MessageBox.Show("Seleccione un usuario para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show($"¿Eliminar usuario '{_usuarioSeleccionado.Nombre}'? Esta acción no se puede deshacer.", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes)
                return;

            try
            {
                _usuarioService.EliminarUsuario(_usuarioSeleccionado.Id);
                CargarUsuarios();
                LimpiarCampos();
                MessageBox.Show("Usuario eliminado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Limpia los campos del formulario y la selección
        private void btnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        // Al seleccionar un usuario en el DataGrid, carga sus datos en el formulario
        private void dgUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsuarios.SelectedItem is Usuario usuario)
            {
                _usuarioSeleccionado = usuario;
                txtNombre.Text = usuario.Nombre;
                txtUsuario.Text = usuario.UsuarioNombre;
                txtClave.Password = usuario.Clave;
                cbRol.SelectedIndex = usuario.Rol == "Administrador" ? 0 : 1;
            }
        }

        // Limpia los campos de entrada y la selección
        private void LimpiarCampos()
        {
            txtNombre.Clear();
            txtUsuario.Clear();
            txtClave.Clear();
            cbRol.SelectedIndex = -1;
            _usuarioSeleccionado = null!;
        }
    }
}