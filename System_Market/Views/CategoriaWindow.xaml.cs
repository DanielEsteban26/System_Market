using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System_Market.Data;
using System_Market.Models;
using System_Market.Services;

namespace System_Market.Views
{
    public partial class CategoriaWindow : Window
    {
        // Servicio para operaciones CRUD de categorías
        private readonly CategoriaService _categoriaService;

        public CategoriaWindow()
        {
            InitializeComponent();
            // Inicializa el servicio con la cadena de conexión y carga las categorías al abrir la ventana
            _categoriaService = new CategoriaService(DatabaseInitializer.GetConnectionString());
            CargarCategorias();
        }

        // Carga todas las categorías desde la base de datos y las muestra en el DataGrid
        private void CargarCategorias()
        {
            List<Categoria> categorias = _categoriaService.ObtenerTodas();
            dgCategorias.ItemsSource = categorias;
        }

        // Evento: Agregar nueva categoría
        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = (txtNombreCategoria.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("El nombre no puede estar vacío.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validación: no permitir duplicados (insensible a mayúsculas/minúsculas)
            var existentes = _categoriaService.ObtenerTodas();
            if (existentes.Exists(c => string.Equals(c.Nombre?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe una categoría con ese nombre.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirmación antes de agregar
            var confirmar = MessageBox.Show($"¿Desea agregar la categoría '{nombre}'?", "Confirmar agregado", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                var categoria = new Categoria { Nombre = nombre };
                _categoriaService.AgregarCategoria(categoria);
                CargarCategorias();
                txtNombreCategoria.Clear();
                MessageBox.Show("Categoría agregada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar la categoría: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Evento: Actualizar categoría seleccionada
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategorias.SelectedItem is not Categoria categoriaSeleccionada)
            {
                MessageBox.Show("Seleccione una categoría para actualizar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nuevoNombre = (txtNombreCategoria.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nuevoNombre))
            {
                MessageBox.Show("El nombre no puede estar vacío.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var existentes = _categoriaService.ObtenerTodas();
            // Validación: no permitir duplicados en otro registro
            if (existentes.Exists(c => c.Id != categoriaSeleccionada.Id && string.Equals(c.Nombre?.Trim(), nuevoNombre, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe otra categoría con ese nombre.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirmación antes de actualizar
            var confirmar = MessageBox.Show($"¿Desea actualizar la categoría '{categoriaSeleccionada.Nombre}' → '{nuevoNombre}'?", "Confirmar actualización", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                categoriaSeleccionada.Nombre = nuevoNombre;
                _categoriaService.ActualizarCategoria(categoriaSeleccionada);
                CargarCategorias();
                txtNombreCategoria.Clear();
                MessageBox.Show("Categoría actualizada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al actualizar la categoría: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Evento: Eliminar categoría seleccionada
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategorias.SelectedItem is not Categoria categoriaSeleccionada)
            {
                MessageBox.Show("Seleccione una categoría para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirmación antes de eliminar
            var confirmar = MessageBox.Show($"¿Eliminar la categoría '{categoriaSeleccionada.Nombre}'? Esta acción no se puede deshacer.", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                _categoriaService.EliminarCategoria(categoriaSeleccionada.Id);
                CargarCategorias();
                txtNombreCategoria.Clear();
                MessageBox.Show("Categoría eliminada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar la categoría: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Evento: Cuando cambia la selección en el DataGrid, muestra el nombre en el TextBox
        private void dgCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCategorias.SelectedItem is Categoria categoriaSeleccionada)
            {
                txtNombreCategoria.Text = categoriaSeleccionada.Nombre;
            }
            else
            {
                txtNombreCategoria.Clear();
            }
        }
    }
}