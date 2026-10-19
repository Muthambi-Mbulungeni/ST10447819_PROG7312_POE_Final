using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Linq;

namespace SmartX.Desktop.Controls;

public class MiniGraph : Control
{
    public static readonly StyledProperty<double[]> HistoryProperty =
        AvaloniaProperty.Register<MiniGraph, double[]>(nameof(History));

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<MiniGraph, IBrush>(nameof(LineBrush), Brushes.White);

    public double[] History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    static MiniGraph()
    {
        AffectsRender<MiniGraph>(HistoryProperty, LineBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var data = History;
        if (data == null || data.Length < 2) return;

        double max = data.Max();
        double min = data.Min();
        double range = max - min;
        if (range == 0) range = 1;

        double width = Bounds.Width;
        double height = Bounds.Height;
        double stepX = width / (data.Length - 1);

        var pen = new Pen(LineBrush, 2);
        
        for (int i = 0; i < data.Length - 1; i++)
        {
            double x1 = i * stepX;
            double y1 = height - ((data[i] - min) / range * height);
            
            double x2 = (i + 1) * stepX;
            double y2 = height - ((data[i + 1] - min) / range * height);

            context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }
    }
}
