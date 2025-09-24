using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    // Ventana para la gestión de proveedores.
    // Permite agregar, actualizar, eliminar y listar proveedores, validando duplicados y RUC único.
    public partial class ProveedorWindow : Window
    {
        private readonly ProveedorService _proveedorService;
        private Proveedor proveedorSeleccionado = null!;

        public ProveedorWindow()
        {
            InitializeComponent();
            _proveedorService = new ProveedorService(DatabaseInitializer.GetConnectionString());

            // botones deshabilitados hasta seleccionar fila
            btnActualizar.IsEnabled = false;
            btnEliminar.IsEnabled = false;

            CargarProveedores();
        }

        // Carga la lista de proveedores y reinicia la selección y botones
        private void CargarProveedores()
        {
            dgProveedores.ItemsSource = null;
            List<Proveedor> lista = _proveedorService.ObtenerTodos();
            dgProveedores.ItemsSource = lista;

            dgProveedores.SelectedIndex = -1;
            btnActualizar.IsEnabled = false;
            btnEliminar.IsEnabled = false;
        }

        // Agrega un nuevo proveedor validando duplicados y RUC único
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = (txtNombre.Text ?? string.Empty).Trim();
            var ruc = (txtRUC.Text ?? string.Empty).Trim();
            var telefono = (txtTelefono.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lista = _proveedorService.ObtenerTodos();
            if (lista.Any(p => string.Equals(p.Nombre?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(p.RUC?.Trim(), ruc, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(p.Telefono?.Trim(), telefono, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe un proveedor con los mismos Nombre, RUC y Teléfono.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(ruc) && lista.Any(p => !string.IsNullOrEmpty(p.RUC) && string.Equals(p.RUC.Trim(), ruc, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe un proveedor con el mismo RUC.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show("¿Desea agregar este proveedor?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes) return;

            var proveedor = new Proveedor
            {
                Nombre = nombre,
                RUC = ruc,
                Telefono = telefono
            };

            try
            {
                _proveedorService.AgregarProveedor(proveedor);
                CargarProveedores();
                LimpiarCampos();
                MessageBox.Show("Proveedor agregado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar proveedor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Actualiza el proveedor seleccionado validando duplicados y RUC único
        private void btnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is not Proveedor seleccionado)
            {
                MessageBox.Show("Seleccione un proveedor para actualizar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nombre = (txtNombre.Text ?? string.Empty).Trim();
            var ruc = (txtRUC.Text ?? string.Empty).Trim();
            var telefono = (txtTelefono.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lista = _proveedorService.ObtenerTodos();
            if (lista.Any(p => p.Id != seleccionado.Id
                               && string.Equals(p.Nombre?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(p.RUC?.Trim(), ruc, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(p.Telefono?.Trim(), telefono, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe otro proveedor con los mismos Nombre, RUC y Teléfono.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(ruc) && lista.Any(p => p.Id != seleccionado.Id && !string.IsNullOrEmpty(p.RUC) && string.Equals(p.RUC.Trim(), ruc, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("El RUC ya está en uso por otro proveedor.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show("¿Desea actualizar este proveedor?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes) return;

            seleccionado.Nombre = nombre;
            seleccionado.RUC = ruc;
            seleccionado.Telefono = telefono;

            try
            {
                _proveedorService.ActualizarProveedor(seleccionado);
                CargarProveedores();
                LimpiarCampos();
                MessageBox.Show("Proveedor actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al actualizar proveedor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Elimina el proveedor seleccionado tras confirmación
        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is not Proveedor seleccionado)
            {
                MessageBox.Show("Seleccione un proveedor para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmar = MessageBox.Show($"¿Eliminar proveedor '{seleccionado.Nombre}'? Esta acción no se puede deshacer.", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                _proveedorService.EliminarProveedor(seleccionado.Id);
                CargarProveedores();
                LimpiarCampos();
                MessageBox.Show("Proveedor eliminado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar proveedor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Refresca la lista de proveedores
        private void btnRefrescar_Click(object sender, RoutedEventArgs e)
        {
            CargarProveedores();
        }

        // Maneja la selección de la grilla y actualiza los campos y botones
        private void dgProveedores_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor seleccionado)
            {
                proveedorSeleccionado = seleccionado;
                txtNombre.Text = seleccionado.Nombre;
                txtRUC.Text = seleccionado.RUC;
                txtTelefono.Text = seleccionado.Telefono;

                btnActualizar.IsEnabled = true;
                btnEliminar.IsEnabled = true;
            }
            else
            {
                LimpiarCampos();
                btnActualizar.IsEnabled = false;
                btnEliminar.IsEnabled = false;
            }
        }

        // Limpia los campos de entrada y la selección de la grilla
        private void LimpiarCampos()
        {
            txtNombre.Text = "";
            txtRUC.Text = "";
            txtTelefono.Text = "";

            try
            {
                dgProveedores.SelectedIndex = -1;
            }
            catch { }
            btnActualizar.IsEnabled = false;
            btnEliminar.IsEnabled = false;
        }
    }
}