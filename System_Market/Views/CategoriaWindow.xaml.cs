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
            if (!string.IsNullOrWhiteSpace(txtNombreCategoria.Text))
            {
                var categoria = new Categoria { Nombre = txtNombreCategoria.Text.Trim() };
                _categoriaService.AgregarCategoria(categoria);
                txtNombreCategoria.Clear();
                CargarCategorias();
            }
            else
            {
                MessageBox.Show("El nombre no puede estar vacío.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategorias.SelectedItem is Categoria categoriaSeleccionada)
            {
                categoriaSeleccionada.Nombre = txtNombreCategoria.Text.Trim();
                _categoriaService.ActualizarCategoria(categoriaSeleccionada);
                txtNombreCategoria.Clear();
                CargarCategorias();
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategorias.SelectedItem is Categoria categoriaSeleccionada)
            {
                _categoriaService.EliminarCategoria(categoriaSeleccionada.Id);
                txtNombreCategoria.Clear();
                CargarCategorias();
            }
        }

        private void dgCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCategorias.SelectedItem is Categoria categoriaSeleccionada)
            {
                txtNombreCategoria.Text = categoriaSeleccionada.Nombre;
            }
        }
    }
}