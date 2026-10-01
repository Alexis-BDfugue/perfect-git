using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SourceGit.Views
{
    /// <summary>
    ///     Renders GitHub-flavored markdown (pull request descriptions) with native controls:
    ///     headings, paragraphs, emphasis, inline code, links, lists and task lists, quotes, code blocks, tables.
    ///     Raw HTML is reduced to its text; images are shown as links.
    /// </summary>
    public partial class MarkdownView : StackPanel
    {
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<MarkdownView, string>(nameof(Markdown));

        public string Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        public MarkdownView()
        {
            Orientation = Orientation.Vertical;
            Spacing = 10;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MarkdownProperty)
                Rebuild();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Rebuild();
        }

        private void Rebuild()
        {
            Children.Clear();

            var text = Markdown;
            if (string.IsNullOrWhiteSpace(text) || this.GetVisualRoot() == null)
                return;

            var doc = Markdig.Markdown.Parse(text, s_pipeline);
            foreach (var block in doc)
            {
                var control = RenderBlock(block, Brush("Brush.FG1"));
                if (control != null)
                    Children.Add(control);
            }
        }

        private Control RenderBlock(Block block, IBrush foreground)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    {
                        var text = CreateText(heading.Inline, foreground, HeadingSize(heading.Level));
                        text.FontWeight = FontWeight.Bold;
                        if (heading.Level > 2)
                            return text;

                        return new Border()
                        {
                            Padding = new Thickness(0, 4, 0, 6),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            BorderBrush = Brush("Brush.Border0"),
                            Child = text,
                        };
                    }
                case ParagraphBlock paragraph:
                    return CreateText(paragraph.Inline, foreground, BASE_SIZE);
                case ListBlock list:
                    return RenderList(list, foreground);
                case QuoteBlock quote:
                    return new Border()
                    {
                        Padding = new Thickness(12, 0, 0, 0),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        BorderBrush = Brush("Brush.Border2"),
                        Child = RenderChildren(quote, Brush("Brush.FG2")),
                    };
                case HtmlBlock html:
                    return RenderHtml(html, foreground);
                case CodeBlock code:
                    return new Border()
                    {
                        Padding = new Thickness(12, 10, 12, 2),
                        CornerRadius = new CornerRadius(6),
                        Background = Brush("Brush.Popup"),
                        BorderThickness = new Thickness(1),
                        BorderBrush = Brush("Brush.Border0"),
                        Child = new ScrollViewer()
                        {
                            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                            Content = new SelectableTextBlock()
                            {
                                Text = code.Lines.ToString().TrimEnd('\n', '\r'),
                                Margin = new Thickness(0, 0, 0, 8), // room for the overlay scrollbar of long lines
                                FontFamily = FindFont("Fonts.Monospace"),
                                FontSize = BASE_SIZE - 1,
                                Foreground = Brush("Brush.FG1"),
                            },
                        },
                    };
                case ThematicBreakBlock:
                    return new Border() { Height = 1, Margin = new Thickness(0, 4), Background = Brush("Brush.Border0") };
                case Table table:
                    return RenderTable(table, foreground);
                case LinkReferenceDefinitionGroup:
                    return null;
                case ContainerBlock container:
                    return RenderChildren(container, foreground);
                case LeafBlock { Inline: { } inline }:
                    return CreateText(inline, foreground, BASE_SIZE);
                default:
                    return null;
            }
        }

        private Control RenderHtml(HtmlBlock html, IBrush foreground)
        {
            var raw = html.Lines.ToString();
            if (raw.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                return null;

            // Tags become line breaks ("<summary>More</summary>text" reads "More" then "text").
            var lines = REG_TAG().Replace(raw, "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var text = string.Join('\n', lines);
            if (text.Length == 0)
                return null;

            return new SelectableTextBlock()
            {
                Text = System.Net.WebUtility.HtmlDecode(text),
                TextWrapping = TextWrapping.Wrap,
                FontSize = BASE_SIZE,
                Foreground = foreground,
            };
        }

        private StackPanel RenderChildren(ContainerBlock container, IBrush foreground)
        {
            var panel = new StackPanel() { Orientation = Orientation.Vertical, Spacing = 8 };
            foreach (var child in container)
            {
                var control = RenderBlock(child, foreground);
                if (control != null)
                    panel.Children.Add(control);
            }

            return panel;
        }

        private Control RenderList(ListBlock list, IBrush foreground)
        {
            var panel = new StackPanel() { Orientation = Orientation.Vertical, Spacing = 4 };
            var index = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;

            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                Control marker;
                var task = FindTaskList(listItem);
                if (task != null)
                {
                    marker = new Border()
                    {
                        Width = 13,
                        Height = 13,
                        Margin = new Thickness(0, 3, 0, 0),
                        CornerRadius = new CornerRadius(3),
                        BorderThickness = new Thickness(1),
                        BorderBrush = task.Checked ? Brush("Brush.Accent") : Brush("Brush.Border2"),
                        Background = task.Checked ? Brush("Brush.Accent") : Brushes.Transparent,
                        Child = task.Checked
                            ? new Avalonia.Controls.Shapes.Path()
                            {
                                Width = 9,
                                Height = 9,
                                Stretch = Stretch.Uniform,
                                Fill = Brush("Brush.AccentFG"),
                                Data = this.FindResource("Icons.Check") as StreamGeometry,
                            }
                            : null,
                    };
                }
                else
                {
                    marker = new TextBlock()
                    {
                        Text = list.IsOrdered ? $"{index}." : "•",
                        FontSize = BASE_SIZE,
                        Foreground = Brush("Brush.FG3"),
                        VerticalAlignment = VerticalAlignment.Top,
                    };
                }

                index++;
                marker.MinWidth = 14;
                marker.VerticalAlignment = VerticalAlignment.Top;

                var content = RenderChildren(listItem, foreground);
                content.Spacing = 4;
                Grid.SetColumn(content, 1);

                var row = new Grid() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                marker.Margin = new Thickness(marker.Margin.Left, marker.Margin.Top, 8, 0);
                row.Children.Add(marker);
                row.Children.Add(content);
                panel.Children.Add(row);
            }

            return panel;
        }

        private Control RenderTable(Table table, IBrush foreground)
        {
            var grid = new Grid();
            var columns = Math.Max(1, table.ColumnDefinitions.Count);
            for (var i = 0; i < columns; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var rowIndex = 0;
            foreach (var r in table)
            {
                if (r is not TableRow row)
                    continue;

                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var col = 0;
                foreach (var c in row)
                {
                    if (c is not TableCell cell || col >= columns)
                        continue;

                    var content = RenderChildren(cell, foreground);
                    if (row.IsHeader)
                    {
                        foreach (var child in content.Children)
                        {
                            if (child is TextBlock tb)
                                tb.FontWeight = FontWeight.SemiBold;
                        }
                    }

                    var border = new Border()
                    {
                        Padding = new Thickness(10, 5),
                        BorderThickness = new Thickness(col == 0 ? 1 : 0, rowIndex == 0 ? 1 : 0, 1, 1),
                        BorderBrush = Brush("Brush.Border2"),
                        Background = row.IsHeader ? Brush("Brush.Hover") : Brushes.Transparent,
                        Child = content,
                    };
                    Grid.SetRow(border, rowIndex);
                    Grid.SetColumn(border, col);
                    grid.Children.Add(border);
                    col++;
                }

                rowIndex++;
            }

            grid.Margin = new Thickness(0, 0, 0, 8); // room for the overlay scrollbar of wide tables
            return new ScrollViewer()
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = grid,
            };
        }

        private static TaskList FindTaskList(ListItemBlock item)
        {
            if (item.Count > 0 && item[0] is ParagraphBlock { Inline: { } inline })
            {
                foreach (var child in inline)
                {
                    if (child is TaskList task)
                        return task;
                }
            }

            return null;
        }

        private MarkdownTextBlock CreateText(ContainerInline inline, IBrush foreground, double fontSize)
        {
            var text = new MarkdownTextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = fontSize,
                Foreground = foreground,
            };

            var builder = new InlineBuilder(this, text, fontSize);
            if (inline != null)
                builder.Add(inline, new InlineStyle());

            text.Inlines = builder.Inlines;
            return text;
        }

        private IBrush Brush(string key)
        {
            return this.FindResource(key) as IBrush ?? Brushes.Gray;
        }

        private FontFamily FindFont(string key)
        {
            return this.FindResource(key) as FontFamily ?? FontFamily.Default;
        }

        private static double HeadingSize(int level) => level switch
        {
            1 => 21,
            2 => 18,
            3 => 15.5,
            4 => 14,
            _ => 13,
        };

        private record struct InlineStyle(bool Bold = false, bool Italic = false, bool Strike = false, string Link = null);

        /// <summary>
        ///     Flattens markdown inlines into runs; hard/soft line breaks become '\n' in the run text so that
        ///     character offsets (used to find the clicked link) match the text of the TextBlock.
        /// </summary>
        private class InlineBuilder
        {
            public InlineCollection Inlines { get; } = new InlineCollection();

            public InlineBuilder(MarkdownView owner, MarkdownTextBlock target, double fontSize)
            {
                _owner = owner;
                _target = target;
                _fontSize = fontSize;
            }

            public void Add(ContainerInline container, InlineStyle style)
            {
                for (var child = container.FirstChild; child != null; child = child.NextSibling)
                    Add(child, style);
            }

            private void Add(Markdig.Syntax.Inlines.Inline inline, InlineStyle style)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        AddRun(literal.Content.ToString(), style, false);
                        break;
                    case EmphasisInline emphasis:
                        {
                            var inner = style;
                            if (emphasis.DelimiterChar == '~')
                                inner.Strike = true;
                            else if (emphasis.DelimiterCount >= 2)
                                inner.Bold = true;
                            else
                                inner.Italic = true;

                            Add(emphasis, inner);
                            break;
                        }
                    case CodeInline code:
                        AddRun(code.Content, style, true);
                        break;
                    case LinkInline { IsImage: true } image:
                        AddRun($"[{ImageLabel(image)}]", style with { Link = image.Url }, false);
                        break;
                    case LinkInline link:
                        {
                            var url = link.GetDynamicUrl?.Invoke() ?? link.Url;
                            if (link.FirstChild == null)
                                AddRun(url, style with { Link = url }, false);
                            else
                                Add(link, style with { Link = url });
                            break;
                        }
                    case AutolinkInline autolink:
                        AddRun(autolink.Url, style with { Link = autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url }, false);
                        break;
                    case LineBreakInline:
                        AddRun("\n", style, false);
                        break;
                    case HtmlInline html:
                        if (html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
                            AddRun("\n", style, false);
                        break;
                    case HtmlEntityInline entity:
                        AddRun(entity.Transcoded.ToString(), style, false);
                        break;
                    case TaskList:
                        break;
                    case ContainerInline container:
                        Add(container, style);
                        break;
                }
            }

            private void AddRun(string text, InlineStyle style, bool isCode)
            {
                if (string.IsNullOrEmpty(text))
                    return;

                var run = new Run(text);
                if (style.Bold)
                    run.FontWeight = FontWeight.SemiBold;
                if (style.Italic)
                    run.FontStyle = FontStyle.Italic;

                if (isCode)
                {
                    run.FontFamily = _owner.FindFont("Fonts.Monospace");
                    run.FontSize = _fontSize - 1;
                    run.Background = _owner.Brush("Brush.Hover");
                }

                if (!string.IsNullOrEmpty(style.Link))
                {
                    run.Foreground = _owner.Brush("Brush.Accent");
                    _target.AddLink(_length, text.Length, style.Link);
                }

                if (style.Strike)
                    run.TextDecorations = TextDecorations.Strikethrough;

                Inlines.Add(run);
                _length += text.Length;
            }

            private static string ImageLabel(LinkInline image)
            {
                var builder = new StringBuilder();
                for (var child = image.FirstChild; child != null; child = child.NextSibling)
                {
                    if (child is LiteralInline literal)
                        builder.Append(literal.Content.ToString());
                }

                return builder.Length > 0 ? builder.ToString() : "image";
            }

            private readonly MarkdownView _owner;
            private readonly MarkdownTextBlock _target;
            private readonly double _fontSize;
            private int _length = 0;
        }

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex REG_TAG();

        private const double BASE_SIZE = 13;

        private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();
    }

    /// <summary>
    ///     Selectable text whose link ranges open in the browser on click (a click that did not select text).
    /// </summary>
    public class MarkdownTextBlock : SelectableTextBlock
    {
        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

        public void AddLink(int start, int length, string url)
        {
            _links.Add((start, start + length, url));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_links.Count > 0)
                Cursor = LinkAt(e.GetPosition(this)) != null ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Ibeam);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (e.InitialPressMouseButton != MouseButton.Left || SelectionStart != SelectionEnd)
                return;

            var url = LinkAt(e.GetPosition(this));
            if (url != null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                                url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)))
            {
                Native.OS.OpenBrowser(url);
                e.Handled = true;
            }
        }

        private string LinkAt(Point point)
        {
            if (_links.Count == 0)
                return null;

            var hit = TextLayout.HitTestPoint(new Point(point.X - Padding.Left, point.Y - Padding.Top));
            if (!hit.IsInside)
                return null;

            foreach (var (start, end, url) in _links)
            {
                if (hit.TextPosition >= start && hit.TextPosition < end)
                    return url;
            }

            return null;
        }

        private readonly List<(int Start, int End, string Url)> _links = [];
    }
}
