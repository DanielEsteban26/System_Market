using LiveChartsCore.SkiaSharpView.Painting;

namespace LiveChartsCore.Kernel.Drawing
{
    // Clase que representa el marco (frame) del área de dibujo del gráfico.
    // Permite definir el relleno y el borde del área donde se dibujan las series.
    internal class DrawMarginFrame : CoreDrawMarginFrame
    {
        // Propiedad para definir el relleno del área de dibujo.
        // Puede ser un color sólido, un gradiente u otro tipo de objeto según la implementación del renderizador.
        public object Fill { get; set; }

        // Propiedad para definir el borde (stroke) del área de dibujo.
        // Utiliza SolidColorPaint de SkiaSharp para especificar el color y grosor del borde.
        public SolidColorPaint Stroke { get; set; }

        // Método que se debe llamar cuando se necesita redibujar el área de dibujo del gráfico.
        // El método base es abstracto, por lo que debe implementarse aquí.
        // Si no se requiere lógica adicional, puede dejarse vacío.
        public override void Invalidate(Chart chart)
        {
            // Aquí se podría agregar lógica para actualizar/redibujar el área de dibujo
            // cuando cambian las propiedades Fill o Stroke, si fuera necesario.
        }
    }
}