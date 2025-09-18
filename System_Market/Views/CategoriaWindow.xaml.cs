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
        private readonly CategoriaService _categoriaService;

        public CategoriaWindow()
        {
            InitializeComponent();
            _categoriaService = new CategoriaService(DatabaseInitializer.GetConnectionString());
            CargarCategorias();
        }

        private void CargarCategorias()
        {
            List<Categoria> categorias = _categoriaService.ObtenerTodas();
            dgCategorias.ItemsSource = categorias;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = (txtNombreCategoria.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("El nombre no puede estar vacío.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validar duplicado por nombre (insensible a mayúsc/minús)
            var existentes = _categoriaService.ObtenerTodas();
            if (existentes.Exists(c => string.Equals(c.Nombre?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe una categoría con ese nombre.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
            // verificar duplicado en otro registro
            if (existentes.Exists(c => c.Id != categoriaSeleccionada.Id && string.Equals(c.Nombre?.Trim(), nuevoNombre, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe otra categoría con ese nombre.", "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategorias.SelectedItem is not Categoria categoriaSeleccionada)
            {
                MessageBox.Show("Seleccione una categoría para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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