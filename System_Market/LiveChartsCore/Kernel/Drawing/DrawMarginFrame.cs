using LiveChartsCore.SkiaSharpView.Painting;

namespace LiveChartsCore.Kernel.Drawing
{
    internal class DrawMarginFrame : CoreDrawMarginFrame
    {
        public object Fill { get; set; }
        public SolidColorPaint Stroke { get; set; }

        public override void Invalidate(Chart chart)
        {
            // El método base es abstracto, no se puede llamar.
            // Si no hay lógica adicional, simplemente deja el método vacío o implementa la lógica necesaria aquí.
        }
    }
}