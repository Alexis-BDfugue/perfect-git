using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SourceGit.Views
{
    public class CommitGraph : Control, Models.IAvatarHost
    {
        public static readonly DirectProperty<CommitGraph, Models.CommitGraph> GraphProperty =
            AvaloniaProperty.RegisterDirect<CommitGraph, Models.CommitGraph>(
                nameof(Graph),
                static o => o.Graph,
                static (o, v) => o.Graph = v);

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetAndRaise(GraphProperty, ref _graph, value);
        }

        public static readonly DirectProperty<CommitGraph, Models.CommitGraphLayout> LayoutProperty =
            AvaloniaProperty.RegisterDirect<CommitGraph, Models.CommitGraphLayout>(
                nameof(Layout),
                static o => o.Layout,
                static (o, v) => o.Layout = v);

        public Models.CommitGraphLayout Layout
        {
            get => _layout;
            set => SetAndRaise(LayoutProperty, ref _layout, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<CommitGraph, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        public CommitGraph()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        }

        public void OnAvatarResourceChanged(string email, Bitmap image)
        {
            if (_avatars.ContainsKey(email))
            {
                _avatars[email] = image;
                InvalidateVisual();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Models.AvatarManager.Instance.Subscribe(this);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            Models.AvatarManager.Instance.Unsubscribe(this);
            base.OnUnloaded(e);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_graph == null || _layout == null)
                return;

            var startY = _layout.StartY;
            var clipWidth = _layout.ClipWidth;
            var clipHeight = Bounds.Height;
            var rowHeight = _layout.RowHeight;
            var endY = startY + clipHeight + rowHeight;

            using (context.PushClip(new Rect(0, 0, clipWidth, clipHeight)))
            using (context.PushTransform(Matrix.CreateTranslation(0, -startY)))
            {
                DrawCurves(context, _graph, startY, endY, rowHeight);
                DrawAnchors(context, _graph, startY, endY, rowHeight);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GraphProperty ||
                change.Property == LayoutProperty ||
                change.Property == DotBrushProperty)
                InvalidateVisual();
        }

        private void DrawCurves(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness, lineCap: PenLineCap.Round);

            foreach (var link in graph.Links)
            {
                var startY = link.Start.Y * rowHeight;
                var endY = link.End.Y * rowHeight;

                if (endY < top)
                    continue;
                if (startY > bottom)
                    break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(link.Start.X, startY), false);
                    ctx.QuadraticBezierTo(new Point(link.Control.X, link.Control.Y * rowHeight), new Point(link.End.X, endY));
                }

                var pen = link.IsHighlighted ? Models.CommitGraph.Pens[link.Color] : grayedPen;
                context.DrawGeometry(null, pen, geo);
            }

            foreach (var line in graph.Paths)
            {
                var last = new Point(line.Points[0].X, line.Points[0].Y * rowHeight);
                var size = line.Points.Count;
                var endY = line.Points[size - 1].Y * rowHeight;

                if (endY < top)
                    continue;
                if (last.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                var pen = line.IsHighlighted ? Models.CommitGraph.Pens[line.Color] : grayedPen;

                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = new Point(line.Points[i].X, line.Points[i].Y * rowHeight);
                        if (cur.Y < top)
                        {
                            last = cur;
                            continue;
                        }

                        if (!started)
                        {
                            ctx.BeginFigure(last, false);
                            started = true;
                        }

                        if (cur.Y > bottom)
                        {
                            cur = new Point(cur.X, bottom);
                            ended = true;
                        }

                        if (cur.X > last.X)
                        {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur);
                        }
                        else if (cur.X < last.X)
                        {
                            if (i < size - 1)
                            {
                                var midY = (last.Y + cur.Y) / 2;
                                ctx.CubicBezierTo(new Point(last.X, midY + rowHeight * 0.2), new Point(cur.X, midY - rowHeight * 0.2), cur);
                            }
                            else
                            {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                            }
                        }
                        else
                        {
                            ctx.LineTo(cur);
                        }

                        if (ended)
                            break;
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }
        }

        private void DrawAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var dotFill = DotBrush;
            var grayedPen = new Pen(Brushes.Gray, Models.CommitGraph.Pens[0].Thickness);
            var initialsTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

            foreach (var dot in graph.Dots)
            {
                var center = new Point(dot.Center.X, dot.Center.Y * rowHeight);

                if (center.Y < top - AVATAR_RADIUS)
                    continue;
                if (center.Y > bottom)
                    break;

                var pen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : grayedPen;
                switch (dot.Type)
                {
                    case Models.CommitGraph.DotType.Merge:
                        context.DrawEllipse(dotFill, pen, center, 5.5, 5.5);
                        break;
                    case Models.CommitGraph.DotType.Head:
                        using (context.PushOpacity(0.45))
                            context.DrawEllipse(null, new Pen(pen.Brush, 1.5), center, 17, 17);
                        DrawAvatar(context, dot, center, pen, initialsTypeface);
                        break;
                    default:
                        DrawAvatar(context, dot, center, pen, initialsTypeface);
                        break;
                }
            }
        }

        private void DrawAvatar(DrawingContext context, Models.CommitGraph.Dot dot, Point center, Pen pen, Typeface typeface)
        {
            var rect = new Rect(center.X - AVATAR_RADIUS, center.Y - AVATAR_RADIUS, AVATAR_RADIUS * 2, AVATAR_RADIUS * 2);

            using (context.PushOpacity(dot.IsHighlighted ? 1 : 0.5))
            {
                var img = GetAvatar(dot.Author);
                if (img != null)
                {
                    using (context.PushGeometryClip(new EllipseGeometry(rect)))
                        context.DrawImage(img, rect);
                }
                else
                {
                    context.DrawEllipse(GetAuthorBrush(dot.Author), null, center, AVATAR_RADIUS, AVATAR_RADIUS);

                    var initials = new FormattedText(
                        GetInitials(dot.Author?.Name),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        11,
                        Brushes.White);
                    context.DrawText(initials, new Point(center.X - initials.Width * 0.5, center.Y - initials.Height * 0.5));
                }
            }

            context.DrawEllipse(null, pen, center, AVATAR_RADIUS, AVATAR_RADIUS);
        }

        private Bitmap GetAvatar(Models.User user)
        {
            if (string.IsNullOrEmpty(user?.Email))
                return null;

            if (_avatars.TryGetValue(user.Email, out var img))
                return img;

            img = Models.AvatarManager.Instance.Request(user.Email, false);
            _avatars[user.Email] = img;
            return img;
        }

        private static IBrush GetAuthorBrush(Models.User user)
        {
            var key = user?.Email ?? string.Empty;
            var hash = 0u;
            foreach (var c in key)
                hash = hash * 31 + c;

            return AUTHOR_COLORS[hash % (uint)AUTHOR_COLORS.Length];
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var words = name.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "?";

            var first = char.ToUpperInvariant(words[0][0]).ToString();
            return words.Length > 1 ? first + char.ToUpperInvariant(words[1][0]) : first;
        }

        private const double AVATAR_RADIUS = 12.5;

        private static readonly IBrush[] AUTHOR_COLORS =
        [
            new SolidColorBrush(Color.Parse("#B25977")),
            new SolidColorBrush(Color.Parse("#B65C4E")),
            new SolidColorBrush(Color.Parse("#AD6723")),
            new SolidColorBrush(Color.Parse("#967600")),
            new SolidColorBrush(Color.Parse("#728426")),
            new SolidColorBrush(Color.Parse("#3D8E53")),
            new SolidColorBrush(Color.Parse("#00917D")),
            new SolidColorBrush(Color.Parse("#008CA1")),
            new SolidColorBrush(Color.Parse("#2382BA")),
            new SolidColorBrush(Color.Parse("#5F75C1")),
            new SolidColorBrush(Color.Parse("#8668B6")),
            new SolidColorBrush(Color.Parse("#A15E9C")),
        ];

        private readonly Dictionary<string, Bitmap> _avatars = new();
        private Models.CommitGraph _graph = null;
        private Models.CommitGraphLayout _layout = null;
    }
}
