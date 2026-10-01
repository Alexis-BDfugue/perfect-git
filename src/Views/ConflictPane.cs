using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    /// <summary>
    ///     One pane of the conflict resolver screen: the Current or Incoming side (lines of each block under a
    ///     "Conflict n" header with its Keep button), or the Result (unresolved blocks as placeholders, kept lines
    ///     striped with the color of their side). Custom-drawn and virtualized: hosted in a ScrollViewer, it only
    ///     draws the visible rows. Its DataContext is the <see cref="ViewModels.MergeConflictEditor"/> of the file.
    /// </summary>
    public class ConflictPane : Control
    {
        public static readonly StyledProperty<List<Models.ConflictPaneRow>> RowsProperty =
            AvaloniaProperty.Register<ConflictPane, List<Models.ConflictPaneRow>>(nameof(Rows));

        public List<Models.ConflictPaneRow> Rows
        {
            get => GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        public static readonly StyledProperty<bool> IsResultProperty =
            AvaloniaProperty.Register<ConflictPane, bool>(nameof(IsResult));

        public bool IsResult
        {
            get => GetValue(IsResultProperty);
            set => SetValue(IsResultProperty, value);
        }

        public static readonly StyledProperty<int> CurrentRegionProperty =
            AvaloniaProperty.Register<ConflictPane, int>(nameof(CurrentRegion));

        public int CurrentRegion
        {
            get => GetValue(CurrentRegionProperty);
            set => SetValue(CurrentRegionProperty, value);
        }

        public static readonly StyledProperty<ViewModels.ConflictSide> CurrentSideProperty =
            AvaloniaProperty.Register<ConflictPane, ViewModels.ConflictSide>(nameof(CurrentSide));

        public ViewModels.ConflictSide CurrentSide
        {
            get => GetValue(CurrentSideProperty);
            set => SetValue(CurrentSideProperty, value);
        }

        public static readonly StyledProperty<ViewModels.ConflictSide> IncomingSideProperty =
            AvaloniaProperty.Register<ConflictPane, ViewModels.ConflictSide>(nameof(IncomingSide));

        public ViewModels.ConflictSide IncomingSide
        {
            get => GetValue(IncomingSideProperty);
            set => SetValue(IncomingSideProperty, value);
        }

        static ConflictPane()
        {
            AffectsMeasure<ConflictPane>(RowsProperty);
            AffectsRender<ConflictPane>(IsResultProperty, CurrentRegionProperty, CurrentSideProperty, IncomingSideProperty);
        }

        public ConflictPane()
        {
            ClipToBounds = true;
        }

        /// <summary>
        ///     Top of the first row of a block, to scroll it into view.
        /// </summary>
        public double GetRegionY(int region)
        {
            var rows = Rows;
            if (rows == null)
                return -1;

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Region == region)
                    return _offsets[i];
            }

            return -1;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged -= OnScrollChanged;

            _scrollViewer = null;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == RowsProperty)
            {
                UpdateOffsets();
                _hovered = null;
            }
            else if (change.Property.Name == nameof(ActualThemeVariant))
            {
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            return new Size(width, _offsets.Count > 0 ? _offsets[^1] + 8 : 0);
        }

        public override void Render(DrawingContext context)
        {
            _zones.Clear();

            var rows = Rows;
            if (rows == null || rows.Count == 0 || _offsets.Count == 0)
                return;

            var top = _scrollViewer?.Offset.Y ?? 0;
            var bottom = top + (_scrollViewer?.Viewport.Height ?? Bounds.Height);
            var width = Bounds.Width;

            var fg1 = Brush("Brush.FG1");
            var fg2 = Brush("Brush.FG2");
            var fg3 = Brush("Brush.FG3");
            var line = Brush("Brush.Border0");
            var raise = Brush("Brush.Popup");
            var accent = Brush("Brush.Accent");
            var accentFG = Brush("Brush.AccentFG");
            var warning = Brush("Brush.Warning");
            var hover = Brush("Brush.Hover");
            var mono = new Typeface(this.FindResource("Fonts.Monospace") as FontFamily ?? FontFamily.Default);
            var ui = new Typeface(this.FindResource("Fonts.Default") as FontFamily ?? FontFamily.Default);
            var uiBold = new Typeface(ui.FontFamily, FontStyle.Normal, FontWeight.SemiBold);
            var current = CurrentSide;
            var incoming = IncomingSide;
            var currentRegion = CurrentRegion;

            var first = FindFirstVisible(top);
            for (var i = first; i < rows.Count; i++)
            {
                var y = _offsets[i] + PADDING_TOP;
                if (y > bottom)
                    break;

                var row = rows[i];
                var side = row.Side == Models.ConflictPaneSide.Current ? current : row.Side == Models.ConflictPaneSide.Incoming ? incoming : null;

                switch (row.Kind)
                {
                    case Models.ConflictPaneRowKind.Line:
                        {
                            var dropped = row.IsDropped;
                            if (side != null)
                            {
                                if (!IsResult && !dropped)
                                    context.FillRectangle(side.Background, new Rect(0, y, width, LINE_HEIGHT));
                                context.FillRectangle(side.Brush, new Rect(0, y, 3, LINE_HEIGHT));
                            }

                            using (context.PushOpacity(dropped ? 0.4 : 1))
                            {
                                if (!string.IsNullOrEmpty(row.LineNumber))
                                {
                                    var no = Text(row.LineNumber, mono, 11, fg3);
                                    context.DrawText(no, new Point(GUTTER - 12 - no.Width, y + (LINE_HEIGHT - no.Height) / 2));
                                }

                                if (!string.IsNullOrEmpty(row.Content))
                                {
                                    var code = Text(row.Content.Replace("\t", "    "), mono, 12, fg1);
                                    var codeY = y + (LINE_HEIGHT - code.Height) / 2;
                                    context.DrawText(code, new Point(GUTTER, codeY));
                                    if (dropped)
                                        context.DrawLine(new Pen(fg3, 1), new Point(GUTTER, y + LINE_HEIGHT / 2), new Point(GUTTER + code.Width, y + LINE_HEIGHT / 2));
                                }
                            }

                            if (IsResult && row.IsKept && side != null)
                                DrawOriginTag(context, row, side, y, width, ui, fg3, fg2, hover);
                            break;
                        }
                    case Models.ConflictPaneRowKind.Header:
                        DrawHeader(context, row, side, y + 4, width, row.Region == currentRegion, ui, uiBold, mono, fg1, fg3, line, raise, accent, accentFG, hover);
                        break;
                    case Models.ConflictPaneRowKind.Placeholder:
                        DrawPlaceholder(context, row, y + 4, width, row.Region == currentRegion, ui, uiBold, mono, fg1, fg2, line, warning, hover);
                        break;
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var zone = HitTest(e.GetPosition(this));
            if (zone?.Bounds != _hovered?.Bounds)
            {
                _hovered = zone;
                Cursor = zone != null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
                InvalidateVisual();
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (_hovered != null)
            {
                _hovered = null;
                Cursor = Cursor.Default;
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var pos = e.GetPosition(this);
            var zone = HitTest(pos);
            if (zone != null)
            {
                if (zone.Toggle == ToggleKind.None)
                    vm.ResolveRegion(zone.Region, zone.Resolution);
                else
                    vm.ToggleSide(zone.Region, zone.Toggle == ToggleKind.Current);
                e.Handled = true;
                return;
            }

            // Clicking a block makes it the target of the keyboard shortcuts.
            var rows = Rows;
            var idx = FindFirstVisible(pos.Y - PADDING_TOP);
            if (rows != null && idx < rows.Count && rows[idx].Region >= 0)
                vm.CurrentRegion = rows[idx].Region;
        }

        private void DrawHeader(DrawingContext context, Models.ConflictPaneRow row, ViewModels.ConflictSide side, double y, double width, bool isCurrent,
            Typeface ui, Typeface uiBold, Typeface mono, IBrush fg1, IBrush fg3, IBrush line, IBrush raise, IBrush accent, IBrush accentFG, IBrush hover)
        {
            var rect = new Rect(0, y, width, HEADER_HEIGHT);
            if (side != null)
            {
                context.FillRectangle(side.Background, rect);
                context.FillRectangle(side.Brush, new Rect(0, y, 3, HEADER_HEIGHT));
            }

            if (isCurrent)
                context.DrawRectangle(null, new Pen(accent, 1), rect.Deflate(new Thickness(3.5, 0.5, 0.5, 0.5)));

            var title = Text(RegionTitle(row), uiBold, 11.5, fg1);
            context.DrawText(title, new Point(14, y + (HEADER_HEIGHT - title.Height) / 2));

            var sub = row.IsKept ? App.Text("ConflictResolver.KeptSub") : row.IsDropped ? App.Text("ConflictResolver.NotKept") : string.Empty;
            if (sub.Length > 0)
            {
                var subText = Text(sub, ui, 11.5, fg3);
                context.DrawText(subText, new Point(14 + title.Width + 8, y + (HEADER_HEIGHT - subText.Height) / 2));
            }

            // Keep button (or "✓ Kept", which undoes when clicked again).
            var isCurrentSide = row.Side == Models.ConflictPaneSide.Current;
            var label = row.IsKept
                ? $"✓ {App.Text("ConflictResolver.Kept")}"
                : App.Text(isCurrentSide ? "ConflictResolver.KeepCurrent" : "ConflictResolver.KeepIncoming");
            var labelText = Text(label, uiBold, 11.5, row.IsKept ? accentFG : fg1);
            var keyText = !row.IsKept && isCurrent ? Text(isCurrentSide ? "⌥←" : "⌥→", mono, 10.5, row.IsKept ? accentFG : fg1) : null;
            var btnWidth = 18 + labelText.Width + (keyText != null ? 6 + keyText.Width : 0);
            var btnRect = new Rect(width - 10 - btnWidth, y + (HEADER_HEIGHT - 22) / 2, btnWidth, 22);
            var zone = AddZone(btnRect, row.Region, Models.ConflictResolution.None, isCurrentSide ? ToggleKind.Current : ToggleKind.Incoming);

            if (row.IsKept && side != null)
                context.DrawRectangle(side.Brush, null, btnRect, 5, 5);
            else
                context.DrawRectangle(raise, new Pen(line, 1), btnRect, 5, 5);

            if (IsHovered(zone))
                context.DrawRectangle(hover, null, btnRect, 5, 5);

            context.DrawText(labelText, new Point(btnRect.X + 9, btnRect.Y + (22 - labelText.Height) / 2));
            if (keyText != null)
            {
                using (context.PushOpacity(0.65))
                    context.DrawText(keyText, new Point(btnRect.X + 9 + labelText.Width + 6, btnRect.Y + (22 - keyText.Height) / 2));
            }
        }

        private void DrawPlaceholder(DrawingContext context, Models.ConflictPaneRow row, double y, double width, bool isCurrent,
            Typeface ui, Typeface uiBold, Typeface mono, IBrush fg1, IBrush fg2, IBrush line, IBrush warning, IBrush hover)
        {
            var warnColor = warning is ISolidColorBrush solid ? solid.Color : Colors.Goldenrod;
            var rect = new Rect(GUTTER, y, Math.Max(0, width - GUTTER - 14), PLACEHOLDER_HEIGHT - 8);
            var border = new Pen(new SolidColorBrush(warnColor, isCurrent ? 0.9 : 0.55), 1, new DashStyle([4, 3], 0));
            context.DrawRectangle(new SolidColorBrush(warnColor, 0.06), border, rect, 7, 7);

            var circle = new Rect(rect.X + 12, rect.Y + (rect.Height - 18) / 2, 18, 18);
            context.DrawEllipse(new SolidColorBrush(warnColor, 0.2), null, circle.Center, 9, 9);
            var num = Text((row.Region + 1).ToString(), uiBold, 11, warning);
            context.DrawText(num, new Point(circle.Center.X - num.Width / 2, circle.Center.Y - num.Height / 2));

            // Keep both button on the right.
            var btnLabel = Text(App.Text("ConflictResolver.KeepBoth"), uiBold, 11.5, fg1);
            var keyText = isCurrent ? Text("⌥↓", mono, 10.5, fg1) : null;
            var btnWidth = 20 + btnLabel.Width + (keyText != null ? 6 + keyText.Width : 0);
            var btnRect = new Rect(rect.Right - 12 - btnWidth, rect.Y + (rect.Height - 24) / 2, btnWidth, 24);
            var zone = AddZone(btnRect, row.Region, Models.ConflictResolution.UseBothMineFirst);
            context.DrawRectangle(IsHovered(zone) ? hover : null, new Pen(line, 1), btnRect, 5, 5);
            context.DrawText(btnLabel, new Point(btnRect.X + 10, btnRect.Y + (24 - btnLabel.Height) / 2));
            if (keyText != null)
            {
                using (context.PushOpacity(0.6))
                    context.DrawText(keyText, new Point(btnRect.X + 10 + btnLabel.Width + 6, btnRect.Y + (24 - keyText.Height) / 2));
            }

            var textX = circle.Right + 12;
            var maxTextWidth = Math.Max(0, btnRect.X - 12 - textX);
            var title = Text(RegionTitle(row), uiBold, 12, fg1, maxTextWidth);
            var hint = Text(App.Text("ConflictResolver.Placeholder.Hint"), ui, 12, fg2, maxTextWidth);
            var textY = rect.Y + (rect.Height - title.Height - hint.Height - 2) / 2;
            context.DrawText(title, new Point(textX, textY));
            context.DrawText(hint, new Point(textX, textY + title.Height + 2));
        }

        private void DrawOriginTag(DrawingContext context, Models.ConflictPaneRow row, ViewModels.ConflictSide side, double y, double width,
            Typeface ui, IBrush fg3, IBrush fg2, IBrush hover)
        {
            var origin = row.Resolution is Models.ConflictResolution.UseBothMineFirst or Models.ConflictResolution.UseBothTheirsFirst
                ? App.Text("ConflictResolver.BothKept")
                : App.Text("ConflictResolver.From", side.Name);
            var tag = Text($"{origin} · {App.Text("ConflictResolver.Undo")}", ui, 11, fg3);
            var rect = new Rect(width - 14 - tag.Width - 8, y + (LINE_HEIGHT - 18) / 2, tag.Width + 8, 18);
            var zone = AddZone(rect, row.Region, Models.ConflictResolution.None);

            context.FillRectangle(Brush("Brush.TitleBar"), rect);
            if (IsHovered(zone))
            {
                context.DrawRectangle(hover, null, rect, 4, 4);
                tag = Text($"{origin} · {App.Text("ConflictResolver.Undo")}", ui, 11, fg2);
            }

            context.DrawText(tag, new Point(rect.X + 4, rect.Y + (18 - tag.Height) / 2));
        }

        private string RegionTitle(Models.ConflictPaneRow row)
        {
            var title = App.Text("ConflictResolver.Conflict", row.Region + 1);
            return string.IsNullOrEmpty(row.Label) ? title : $"{title} · {row.Label}";
        }

        private static FormattedText Text(string text, Typeface typeface, double size, IBrush brush, double maxWidth = 0)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush);
            if (maxWidth > 0)
            {
                formatted.MaxTextWidth = maxWidth;
                formatted.MaxLineCount = 1;
                formatted.Trimming = TextTrimming.CharacterEllipsis;
            }

            return formatted;
        }

        private IBrush Brush(string key)
        {
            return this.FindResource(ActualThemeVariant, key) as IBrush ?? Brushes.Gray;
        }

        private HotZone AddZone(Rect bounds, int region, Models.ConflictResolution resolution, ToggleKind toggle = ToggleKind.None)
        {
            var zone = new HotZone(bounds, region, resolution, toggle);
            _zones.Add(zone);
            return zone;
        }

        private bool IsHovered(HotZone zone)
        {
            return _hovered != null && _hovered.Bounds == zone.Bounds;
        }

        private HotZone HitTest(Point pos)
        {
            foreach (var zone in _zones)
            {
                if (zone.Bounds.Contains(pos))
                    return zone;
            }

            return null;
        }

        private void UpdateOffsets()
        {
            _offsets.Clear();

            var y = 0.0;
            var rows = Rows;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    _offsets.Add(y);
                    y += row.Kind switch
                    {
                        Models.ConflictPaneRowKind.Header => HEADER_HEIGHT + 4,
                        Models.ConflictPaneRowKind.Placeholder => PLACEHOLDER_HEIGHT,
                        _ => LINE_HEIGHT,
                    };
                }
            }

            _offsets.Add(y);
        }

        private int FindFirstVisible(double top)
        {
            // Binary search on the row tops (the last offset is the total height).
            int lo = 0, hi = _offsets.Count - 2;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_offsets[mid] <= top)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return Math.Max(0, lo);
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            InvalidateVisual();
        }

        /// <summary>A Keep button toggles its side of the block (see <see cref="ViewModels.MergeConflictEditor.ToggleSide"/>).</summary>
        private enum ToggleKind
        {
            None,
            Current,
            Incoming,
        }

        private record HotZone(Rect Bounds, int Region, Models.ConflictResolution Resolution, ToggleKind Toggle);

        private const double LINE_HEIGHT = 20;
        private const double HEADER_HEIGHT = 30;
        private const double PLACEHOLDER_HEIGHT = 64;
        private const double PADDING_TOP = 6;
        private const double GUTTER = 41;

        private ScrollViewer _scrollViewer = null;
        private readonly List<double> _offsets = [];
        private readonly List<HotZone> _zones = [];
        private HotZone _hovered = null;
    }
}
