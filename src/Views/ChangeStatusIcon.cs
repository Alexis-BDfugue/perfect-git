using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class ChangeStatusIcon : Control
    {
        // Untracked files read as "A" (new file), like the design's status letters; the row tooltip still says untracked.
        private static readonly string[] INDICATOR = ["?", "M", "T", "A", "D", "R", "C", "A", "!"];
        private static readonly string[] BRUSH =
        [
            "Brush.FG3",
            "Brush.Warning",
            "Brush.Warning",
            "Brush.Added",
            "Brush.Deleted",
            "Brush.Accent",
            "Brush.Warning",
            "Brush.Added",
            "Brush.Deleted",
        ];

        public static readonly DirectProperty<ChangeStatusIcon, bool> IsUnstagedChangeProperty =
            AvaloniaProperty.RegisterDirect<ChangeStatusIcon, bool>(
                nameof(IsUnstagedChange),
                static o => o.IsUnstagedChange,
                static (o, v) => o.IsUnstagedChange = v);

        public bool IsUnstagedChange
        {
            get => _isUnstagedChange;
            set => SetAndRaise(IsUnstagedChangeProperty, ref _isUnstagedChange, value);
        }

        public static readonly DirectProperty<ChangeStatusIcon, Models.Change> ChangeProperty =
            AvaloniaProperty.RegisterDirect<ChangeStatusIcon, Models.Change>(
                nameof(Change),
                static o => o.Change,
                static (o, v) => o.Change = v);

        public Models.Change Change
        {
            get => _change;
            set => SetAndRaise(ChangeProperty, ref _change, value);
        }

        public override void Render(DrawingContext context)
        {
            if (_change == null || Bounds.Width <= 0)
                return;

            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
            var idx = (int)(_isUnstagedChange ? _change.WorkTree : _change.Index);
            var brush = this.FindResource(BRUSH[idx]) as IBrush ?? Brushes.Gray;

            var txt = new FormattedText(
                INDICATOR[idx],
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Min(Bounds.Width, Bounds.Height) * 0.72,
                brush);

            var textOrigin = new Point((Bounds.Width - txt.Width) * 0.5, (Bounds.Height - txt.Height) * 0.5);
            context.DrawText(txt, textOrigin);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsUnstagedChangeProperty || change.Property == ChangeProperty)
                InvalidateVisual();
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                InvalidateVisual();
        }

        private bool _isUnstagedChange = false;
        private Models.Change _change = null;
    }
}
