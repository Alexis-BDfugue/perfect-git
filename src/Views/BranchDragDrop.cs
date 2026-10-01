using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    /// <summary>
    ///     Drag a branch (sidebar row or graph pill) onto another one to merge or rebase it.
    ///     The drag stays inside the window, so it is driven by the pointer instead of the OS drag and drop
    ///     (which adds its own document icon next to the ghost on macOS). The usual DragDrop.DragOver/DragLeave/Drop
    ///     events are still raised on the element under the pointer, so drop targets handle them as usual.
    /// </summary>
    public static class BranchDragDrop
    {
        public static Models.Branch Dragging { get; private set; }

        public static async Task StartAsync(PointerEventArgs trigger, Visual origin, Models.Branch branch, Action<bool> setSourceDimmed)
        {
            if (Dragging != null || TopLevel.GetTopLevel(origin) is not { } host)
                return;

            Dragging = branch;
            setSourceDimmed?.Invoke(true);
            ShowGhost(origin, branch, trigger);

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(s_format, branch.FullName));

            var done = new TaskCompletionSource();
            Interactive over = null;

            DragEventArgs Raise(RoutedEvent<DragEventArgs> routedEvent, Interactive target, Point pos, KeyModifiers modifiers)
            {
                var args = new DragEventArgs(routedEvent, data, host, pos, modifiers) { DragEffects = DragDropEffects.Move };
                target.RaiseEvent(args);
                return args;
            }

            void Track(PointerEventArgs e)
            {
                var pos = e.GetPosition(host);
                MoveGhostTo(e.GetPosition(s_ghostLayer));

                var target = FindDropTarget(host, pos);
                if (!ReferenceEquals(target, over))
                {
                    if (over != null)
                        Raise(DragDrop.DragLeaveEvent, over, pos, e.KeyModifiers);

                    over = target;
                    if (over != null)
                        Raise(DragDrop.DragEnterEvent, over, pos, e.KeyModifiers);
                }

                if (over != null)
                    Raise(DragDrop.DragOverEvent, over, pos, e.KeyModifiers);
            }

            void OnMoved(object _, PointerEventArgs e)
            {
                e.Handled = true;
                Track(e);
            }

            void OnReleased(object _, PointerReleasedEventArgs e)
            {
                e.Handled = true;
                Track(e);

                if (over != null)
                {
                    var target = over;
                    over = null;
                    Raise(DragDrop.DropEvent, target, e.GetPosition(host), e.KeyModifiers);
                }

                done.TrySetResult();
            }

            void OnCaptureLost(object _, PointerCaptureLostEventArgs e) => done.TrySetResult();

            void OnKeyDown(object _, KeyEventArgs e)
            {
                if (e.Key != Key.Escape)
                    return;

                e.Handled = true;
                done.TrySetResult();
            }

            host.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, true);
            host.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, true);
            host.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct);
            host.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            trigger.Pointer.Capture(host);

            try
            {
                await done.Task;
            }
            finally
            {
                host.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
                host.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
                host.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
                host.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
                if (ReferenceEquals(trigger.Pointer.Captured, host))
                    trigger.Pointer.Capture(null);

                // Cancelled (Esc, capture lost): let the hovered target clear its highlight.
                if (over != null)
                    Raise(DragDrop.DragLeaveEvent, over, default, KeyModifiers.None);

                setSourceDimmed?.Invoke(false);
                ClearTarget();
                HideGhost();
                Dragging = null;
            }
        }

        public static bool CanDropOn(Models.Branch target)
        {
            return Dragging != null &&
                target != null &&
                !target.FullName.Equals(Dragging.FullName, StringComparison.Ordinal);
        }

        /// <summary>
        ///     Remember the highlighted drop target, so it can be cleared when the pointer leaves it or the drag ends.
        /// </summary>
        public static void SetTarget(object key, Action clear)
        {
            if (ReferenceEquals(key, s_targetKey))
                return;

            ClearTarget();
            s_targetKey = key;
            s_clearTarget = clear;
        }

        public static void ClearTarget()
        {
            s_clearTarget?.Invoke();
            s_clearTarget = null;
            s_targetKey = null;
        }

        private static void MoveGhostTo(Point p)
        {
            if (s_ghostTranslate == null)
                return;

            s_ghostTranslate.X = p.X + 14;
            s_ghostTranslate.Y = p.Y + 6;
        }

        private static Interactive FindDropTarget(TopLevel host, Point pos)
        {
            // Same lookup as the OS drag and drop: the deepest element under the pointer that accepts drops.
            for (var v = host.InputHitTest(pos) as Visual; v != null; v = v.GetVisualParent())
            {
                if (v is Interactive target && DragDrop.GetAllowDrop(target))
                    return target;
            }

            return null;
        }

        public static void ShowPopover(Control anchor, ViewModels.Repository repo, Models.Branch source, Models.Branch target, double offsetX)
        {
            var view = new BranchDropPopover()
            {
                DataContext = new ViewModels.BranchDrop(repo, source, target),
                Anchor = anchor,
            };

            var flyout = new Flyout()
            {
                Content = view,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                HorizontalOffset = offsetX - 12,
                ShowMode = FlyoutShowMode.Standard,
            };
            flyout.FlyoutPresenterClasses.Add("branch_drop");
            view.CloseRequested = () => flyout.Hide();

            // Wait for the drag operation to finish before opening a popup.
            Dispatcher.UIThread.Post(() => flyout.ShowAt(anchor));
        }

        private static void ShowGhost(Visual origin, Models.Branch branch, PointerEventArgs trigger)
        {
            // The adorner layer lives inside the window template: look it up from the dragged visual, not the TopLevel.
            var layer = AdornerLayer.GetAdornerLayer(origin);
            if (layer == null)
                return;

            var laneBrush = Models.BranchColor.BrushOf(branch.Name);
            var laneColor = (laneBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            var start = trigger.GetPosition(layer);

            s_ghostTranslate = new TranslateTransform(start.X + 14, start.Y + 6);
            s_ghost = new Border()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = laneBrush,
                Background = layer.FindResource("Brush.Popup") as IBrush,
                BoxShadow = BoxShadows.Parse("0 10 24 0 #AA000000"),
                IsHitTestVisible = false,
                RenderTransform = new TransformGroup() { Children = { new RotateTransform(-3), s_ghostTranslate } },
                Child = new Border()
                {
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(laneColor, 0.3),
                    Padding = new Thickness(10, 4),
                    Child = new TextBlock()
                    {
                        Text = $"⠿ {branch.FriendlyName}",
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = layer.FindResource("Brush.FG1") as IBrush,
                    },
                },
            };

            // Not an adorned element: the layer would replace RenderTransform (and drop the tilt) to follow it.
            layer.Children.Add(s_ghost);
            s_ghostLayer = layer;
        }

        private static void HideGhost()
        {
            if (s_ghost != null)
                s_ghostLayer?.Children.Remove(s_ghost);

            s_ghost = null;
            s_ghostLayer = null;
        }

        private static readonly DataFormat<string> s_format = DataFormat.CreateStringApplicationFormat("perfect-git-branch");
        private static Border s_ghost = null;
        private static AdornerLayer s_ghostLayer = null;
        private static TranslateTransform s_ghostTranslate = null;
        private static object s_targetKey = null;
        private static Action s_clearTarget = null;
    }
}
