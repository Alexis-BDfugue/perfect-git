using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    /// <summary>
    ///     Dashed node drawn for the "uncommitted changes" row, linked to HEAD below it.
    /// </summary>
    public class WipGraphMarker : Control
    {
        public static readonly StyledProperty<IBrush> LaneBrushProperty =
            AvaloniaProperty.Register<WipGraphMarker, IBrush>(nameof(LaneBrush), Brushes.Gray);

        public IBrush LaneBrush
        {
            get => GetValue(LaneBrushProperty);
            set => SetValue(LaneBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> FillProperty =
            AvaloniaProperty.Register<WipGraphMarker, IBrush>(nameof(Fill), Brushes.Transparent);

        public IBrush Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        static WipGraphMarker()
        {
            AffectsRender<WipGraphMarker>(LaneBrushProperty, FillProperty);
        }

        public override void Render(DrawingContext context)
        {
            var x = Models.CommitGraph.FIRST_LANE_X;
            var cy = Bounds.Height * 0.5;
            var edge = new Pen(LaneBrush, 2.5, new DashStyle([1.6, 1.6], 0));
            var node = new Pen(LaneBrush, 2, new DashStyle([1.5, 1.5], 0));

            context.DrawLine(edge, new Point(x, cy + 10), new Point(x, Bounds.Height));
            context.DrawEllipse(Fill, node, new Point(x, cy), 10, 10);
        }
    }
}
