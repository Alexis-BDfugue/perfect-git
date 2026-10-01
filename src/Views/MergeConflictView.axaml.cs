using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using AvaloniaEdit.Rendering;

namespace SourceGit.Views
{
    /// <summary>
    ///     The three-way conflict resolver (mine, theirs, result and minimap), hosted by the
    ///     <see cref="MergeConflictEditor"/> window and by the conflict view in the center of the main window.
    /// </summary>
    public partial class MergeConflictView : UserControl
    {
        public static readonly DirectProperty<MergeConflictView, Models.ConflictSelectedChunk> SelectedChunkProperty =
            AvaloniaProperty.RegisterDirect<MergeConflictView, Models.ConflictSelectedChunk>(
                nameof(SelectedChunk),
                static o => o.SelectedChunk,
                static (o, v) => o.SelectedChunk = v);

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetAndRaise(SelectedChunkProperty, ref _selectedChunk, value);
        }

        public MergeConflictView()
        {
            InitializeComponent();
            Bind(SelectedChunkProperty, new Binding("SelectedChunk", BindingMode.OneWay));
        }

        public void GotoPrevConflict()
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.UnsolvedCount > 0)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursLines;
                var minY = double.MaxValue;
                var minLineIdx = lines.Count;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY < minY)
                        {
                            minY = startY;
                            minLineIdx = lineIndex;
                        }
                    }

                    for (var i = vm.ConflictRegions.Count - 1; i >= 0; i--)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal < minLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }
        }

        public void GotoNextConflict()
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.UnsolvedCount > 0)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursLines;
                var maxY = 0.0;
                var maxLineIdx = 0;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY > maxY)
                        {
                            maxY = startY;
                            maxLineIdx = lineIndex;
                        }
                    }

                    for (var i = 0; i < vm.ConflictRegions.Count; i++)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal > maxLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedChunkProperty)
                UpdatePopupVisibility();
        }

        private void UpdatePopupVisibility()
        {
            // Hide all popups first
            MinePopup.IsVisible = false;
            TheirsPopup.IsVisible = false;
            ResultPopup.IsVisible = false;
            ResultUndoPopup.IsVisible = false;

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var chunk = vm.SelectedChunk;
            if (chunk == null)
                return;

            // Get the presenter for bounds checking
            var presenter = chunk.Panel switch
            {
                Models.ConflictPanelType.Ours => OursPresenter,
                Models.ConflictPanelType.Theirs => TheirsPresenter,
                Models.ConflictPanelType.Result => ResultPresenter,
                _ => null
            };

            // Show the appropriate popup based on panel type and resolved state
            var popup = chunk.Panel switch
            {
                Models.ConflictPanelType.Ours => MinePopup,
                Models.ConflictPanelType.Theirs => TheirsPopup,
                Models.ConflictPanelType.Result => chunk.IsResolved ? ResultUndoPopup : ResultPopup,
                _ => null
            };

            if (popup != null && presenter != null)
            {
                // Position popup - clamp to visible area
                var top = chunk.Y + (chunk.Height >= 36 ? 8 : 2);

                // Clamp top to ensure popup is visible
                var popupHeight = popup.Bounds.Height > 0 ? popup.Bounds.Height : 32;
                var presenterHeight = presenter.Bounds.Height;
                top = Math.Max(4, Math.Min(top, presenterHeight - popupHeight - 4));

                popup.Margin = new Thickness(0, top, 24, 0);
                popup.IsVisible = true;
            }
        }

        private Models.ConflictSelectedChunk _selectedChunk = null;
    }
}
