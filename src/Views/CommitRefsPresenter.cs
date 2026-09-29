using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitRefsIconCache
    {
        public static CommitRefsIconCache Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CommitRefsIconCache();
                return _instance;
            }
        }

        public CommitRefsIconCache()
        {
            _head = LoadIcon("Icons.Head");
            _branch = LoadIcon("Icons.Branch");
            _remote = LoadIcon("Icons.Remote");
            _tag = LoadIcon("Icons.Tag");
        }

        public Geometry GetIcon(Models.DecoratorType type)
        {
            return type switch
            {
                Models.DecoratorType.CurrentBranchHead => _head,
                Models.DecoratorType.CurrentCommitHead => _head,
                Models.DecoratorType.LocalBranchHead => _branch,
                Models.DecoratorType.RemoteBranchHead => _remote,
                Models.DecoratorType.Tag => _tag,
                _ => null,
            };
        }

        private Geometry LoadIcon(string resourceKey)
        {
            var geo = App.Current.FindResource(resourceKey) as StreamGeometry;
            var drawGeo = geo!.Clone();
            var iconBounds = drawGeo.Bounds;
            var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
            var scale = Math.Min(10.0 / iconBounds.Width, 10.0 / iconBounds.Height);
            var transform = translation * Matrix.CreateScale(scale, scale);
            if (drawGeo.Transform == null || drawGeo.Transform.Value == Matrix.Identity)
                drawGeo.Transform = new MatrixTransform(transform);
            else
                drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

            return drawGeo;
        }

        private static CommitRefsIconCache _instance = null;
        private Geometry _head = null;
        private Geometry _branch = null;
        private Geometry _remote = null;
        private Geometry _tag = null;
    }

    public class CommitRefsPresenter : Control
    {
        public class RenderItem
        {
            public Models.Decorator Decorator { get; set; } = null;
            public FormattedText Label { get; set; } = null;
            public IBrush Brush { get; set; } = null;
            public bool IsHead { get; set; } = false;
            public double Width { get; set; } = 0.0;
            public List<FormattedText> Remotes { get; set; } = [];
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<CommitRefsPresenter>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<CommitRefsPresenter>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Background), Brushes.Transparent);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<bool> UseCompactBranchNamesProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseCompactBranchNames));

        public bool UseCompactBranchNames
        {
            get => GetValue(UseCompactBranchNamesProperty);
            set => SetValue(UseCompactBranchNamesProperty, value);
        }

        public static readonly StyledProperty<bool> HasSingleRemoteProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(HasSingleRemote));

        public bool HasSingleRemote
        {
            get => GetValue(HasSingleRemoteProperty);
            set => SetValue(HasSingleRemoteProperty, value);
        }

        public static readonly StyledProperty<bool> UseGraphColorProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseGraphColor));

        public bool UseGraphColor
        {
            get => GetValue(UseGraphColorProperty);
            set => SetValue(UseGraphColorProperty, value);
        }

        public static readonly StyledProperty<bool> AllowWrapProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(AllowWrap));

        public bool AllowWrap
        {
            get => GetValue(AllowWrapProperty);
            set => SetValue(AllowWrapProperty, value);
        }

        public static readonly StyledProperty<bool> ShowTagsProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(ShowTags), true);

        public bool ShowTags
        {
            get => GetValue(ShowTagsProperty);
            set => SetValue(ShowTagsProperty, value);
        }

        public Models.Decorator DecoratorAt(Point point)
        {
            var x = 0.0;
            foreach (var item in _items)
            {
                x += item.Width + PILL_GAP;
                if (point.X < x)
                    return item.Decorator;
            }

            return null;
        }

        public override void Render(DrawingContext context)
        {
            if (_items.Count == 0)
                return;

            var allowWrap = AllowWrap;
            var x = 0.5;
            var y = 0.5;
            var remoteIcon = CommitRefsIconCache.Instance.GetIcon(Models.DecoratorType.RemoteBranchHead);
            var remoteBrush = this.FindResource("Brush.FG2") as IBrush ?? Brushes.Gray;

            context.FillRectangle(Brushes.Transparent, Bounds);

            foreach (var item in _items)
            {
                if (allowWrap && x > 0.5 && x + item.Width > Bounds.Width)
                {
                    x = 0.5;
                    y += PILL_HEIGHT + 4;
                }

                var rect = new RoundedRect(new Rect(x, y, item.Width, PILL_HEIGHT), new CornerRadius(5));
                switch (item.Decorator.Type)
                {
                    case Models.DecoratorType.RemoteBranchHead:
                        using (context.PushOpacity(.5))
                            context.DrawRectangle(null, new Pen(item.Brush), rect);
                        break;
                    case Models.DecoratorType.Tag:
                        using (context.PushOpacity(.14))
                            context.DrawRectangle(item.Brush, null, rect);
                        break;
                    default:
                        using (context.PushOpacity(.22))
                            context.DrawRectangle(item.Brush, null, rect);
                        break;
                }

                var tx = x + PILL_PADDING;
                context.DrawText(item.Label, new Point(tx, y + (PILL_HEIGHT - item.Label.Height) * 0.5));

                if (item.Remotes.Count > 0)
                {
                    var rx = tx + item.Label.WidthIncludingTrailingWhitespace + 6;
                    var iconY = y + (PILL_HEIGHT - 10) * 0.5;

                    if (HasSingleRemote)
                    {
                        using (context.PushTransform(Matrix.CreateTranslation(rx, iconY)))
                            context.DrawGeometry(remoteBrush, null, remoteIcon);
                    }
                    else
                    {
                        foreach (var remote in item.Remotes)
                        {
                            using (context.PushTransform(Matrix.CreateTranslation(rx, iconY)))
                                context.DrawGeometry(remoteBrush, null, remoteIcon);
                            context.DrawText(remote, new Point(rx + 13, y + (PILL_HEIGHT - remote.Height) * 0.5));
                            rx += remote.WidthIncludingTrailingWhitespace + 19;
                        }
                    }
                }

                x += item.Width + PILL_GAP;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == ForegroundProperty ||
                change.Property == UseGraphColorProperty ||
                change.Property == UseCompactBranchNamesProperty ||
                change.Property == HasSingleRemoteProperty ||
                change.Property == BackgroundProperty ||
                change.Property == ShowTagsProperty)
                InvalidateMeasure();
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                InvalidateMeasure();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _items.Clear();

            if (DataContext is not Models.Commit commit)
                return new Size(0, 0);

            var refs = commit.Decorators;
            var count = refs.Count;
            if (count == 0)
            {
                InvalidateVisual();
                return new Size(0, 0);
            }

            var useCompactBranchNames = UseCompactBranchNames;
            var hasSingleRemote = HasSingleRemote;
            var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold);
            var fg = Foreground;
            var remoteFG = this.FindResource("Brush.FG2") as IBrush ?? fg;
            var tagBrush = this.FindResource("Brush.Warning") as IBrush ?? Brushes.Goldenrod;
            var laneBrush = UseGraphColor ? Models.CommitGraph.Pens[commit.Color].Brush : Brushes.Gray;
            var labelSize = FontSize;
            var requiredHeight = PILL_HEIGHT;
            var x = 0.0;
            var allowWrap = AllowWrap;
            var showTags = ShowTags;
            var skippedIdx = new HashSet<int>();

            for (var i = 0; i < count; i++)
            {
                if (skippedIdx.Contains(i))
                    continue;

                var decorator = refs[i];
                if (!showTags && decorator.Type == Models.DecoratorType.Tag)
                    continue;

                var item = new RenderItem()
                {
                    Decorator = decorator,
                    Brush = decorator.Type == Models.DecoratorType.Tag ? tagBrush : laneBrush,
                    IsHead = decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.CurrentCommitHead,
                };
                _items.Add(item);

                var labelFG = decorator.Type switch
                {
                    Models.DecoratorType.RemoteBranchHead => remoteFG,
                    Models.DecoratorType.Tag => tagBrush,
                    _ => fg,
                };

                item.Label = new FormattedText(
                    item.IsHead ? $"✓ {decorator.Name}" : decorator.Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    labelSize,
                    labelFG);

                item.Width = item.Label.Width + PILL_PADDING * 2;

                var findRemotes = useCompactBranchNames && (decorator.Type == Models.DecoratorType.CurrentBranchHead || decorator.Type == Models.DecoratorType.LocalBranchHead);
                if (findRemotes)
                {
                    for (var j = i + 1; j < count; j++)
                    {
                        var test = refs[j];
                        if (test.Type != Models.DecoratorType.RemoteBranchHead)
                            continue;

                        var idxOfSlash = test.Name.IndexOf('/');
                        if (idxOfSlash < 1 || idxOfSlash == test.Name.Length - 1)
                            continue;

                        var name = test.Name.Substring(idxOfSlash + 1);
                        if (decorator.Name.Equals(name, StringComparison.Ordinal))
                        {
                            var remote = new FormattedText(
                                test.Name.Substring(0, idxOfSlash),
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                labelSize,
                                remoteFG);

                            item.Remotes.Add(remote);

                            if (hasSingleRemote)
                                item.Width += 16;
                            else
                                item.Width += remote.WidthIncludingTrailingWhitespace + 19;

                            skippedIdx.Add(j);
                        }
                    }
                }

                x += item.Width + PILL_GAP;
                if (allowWrap)
                {
                    if (x > availableSize.Width)
                    {
                        requiredHeight += PILL_HEIGHT + 4;
                        x = item.Width;
                    }
                }
            }

            double requiredWidth = 0;
            if (_items.Count > 0)
            {
                if (allowWrap && requiredHeight > PILL_HEIGHT)
                    requiredWidth = double.IsInfinity(availableSize.Width) ? x + 2 : availableSize.Width;
                else
                    requiredWidth = x + 2;
            }

            InvalidateVisual();
            return new Size(requiredWidth, requiredHeight);
        }

        private const double PILL_HEIGHT = 18;
        private const double PILL_PADDING = 8;
        private const double PILL_GAP = 6;

        private List<RenderItem> _items = new List<RenderItem>();
    }
}
