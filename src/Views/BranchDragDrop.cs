using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace SourceGit.Views
{
    /// <summary>
    ///     Drag a branch (sidebar row or graph pill) onto another one to merge or rebase it.
    /// </summary>
    public static class BranchDragDrop
    {
        public static Models.Branch Dragging { get; private set; }

        public static async Task StartAsync(PointerEventArgs trigger, Visual origin, Models.Branch branch, Action<bool> setSourceDimmed)
        {
            if (Dragging != null)
                return;

            Dragging = branch;
            setSourceDimmed?.Invoke(true);
            ShowGhost(origin, branch, trigger);

            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(s_format, branch.FullName));
                await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
            }
            finally
            {
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

        public static void MoveGhost(DragEventArgs e)
        {
            if (s_ghost == null || s_ghostHost == null)
                return;

            var p = e.GetPosition(s_ghostHost);
            s_ghostTranslate.X = p.X + 14;
            s_ghostTranslate.Y = p.Y + 6;
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
            var host = TopLevel.GetTopLevel(origin);
            var layer = host != null ? AdornerLayer.GetAdornerLayer(host) : null;
            if (host == null || layer == null)
                return;

            var laneBrush = Models.BranchColor.BrushOf(branch.Name);
            var laneColor = (laneBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            var start = trigger.GetPosition(host);

            s_ghostTranslate = new TranslateTransform(start.X + 14, start.Y + 6);
            s_ghost = new Border()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = laneBrush,
                Background = host.FindResource("Brush.Popup") as IBrush,
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
                        Foreground = host.FindResource("Brush.FG1") as IBrush,
                    },
                },
            };

            AdornerLayer.SetAdornedElement(s_ghost, host);
            layer.Children.Add(s_ghost);
            s_ghostHost = host;
            s_ghostLayer = layer;
        }

        private static void HideGhost()
        {
            if (s_ghost != null)
                s_ghostLayer?.Children.Remove(s_ghost);

            s_ghost = null;
            s_ghostLayer = null;
            s_ghostHost = null;
        }

        private static readonly DataFormat<string> s_format = DataFormat.CreateStringApplicationFormat("perfect-git-branch");
        private static Border s_ghost = null;
        private static AdornerLayer s_ghostLayer = null;
        private static Visual s_ghostHost = null;
        private static TranslateTransform s_ghostTranslate = null;
        private static object s_targetKey = null;
        private static Action s_clearTarget = null;
    }
}
