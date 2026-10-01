using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class ConflictResolver : UserControl
    {
        public ConflictResolver()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Focus();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_vm != null)
                _vm.PropertyChanged -= OnResolverPropertyChanged;

            _vm = DataContext as ViewModels.ConflictResolver;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnResolverPropertyChanged;
                foreach (var pane in new[] { CurrentPane, IncomingPane, ResultPane })
                {
                    pane.CurrentSide = _vm.Current;
                    pane.IncomingSide = _vm.Incoming;
                }
            }

            WatchEditor();
        }

        private void OnResolverPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.ConflictResolver.Editor))
            {
                WatchEditor();
                CurrentScroller.Offset = default;
                ResultScroller.Offset = default;
            }
        }

        private void WatchEditor()
        {
            if (_editor != null)
                _editor.PropertyChanged -= OnEditorPropertyChanged;

            _editor = _vm?.Editor;
            if (_editor != null)
                _editor.PropertyChanged += OnEditorPropertyChanged;
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.CurrentRegion) && _editor != null)
            {
                var region = _editor.CurrentRegion;
                Dispatcher.UIThread.Post(() => ScrollToRegion(region), DispatcherPriority.Background);
            }
        }

        /// <summary>
        ///     Brings a block into view in the side panes and in the result, when it is out of sight.
        /// </summary>
        private void ScrollToRegion(int region)
        {
            BringIntoView(CurrentScroller, CurrentPane.GetRegionY(region));
            BringIntoView(ResultScroller, ResultPane.GetRegionY(region));
        }

        private static void BringIntoView(ScrollViewer scroller, double y)
        {
            if (y < 0)
                return;

            var top = scroller.Offset.Y;
            var height = scroller.Viewport.Height;
            if (y < top + 8 || y > top + height - 60)
                scroller.Offset = new Vector(0, Math.Max(0, y - 40));
        }

        private void OnSideScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Both sides keep their blocks aligned: scroll them together.
            if (_syncingScroll || sender is not ScrollViewer source)
                return;

            _syncingScroll = true;
            var other = source == CurrentScroller ? IncomingScroller : CurrentScroller;
            other.Offset = source.Offset;
            _syncingScroll = false;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_vm == null)
                return;

            var cmd = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (_vm.IsAbortConfirmVisible)
            {
                if (e.Key == Key.Escape)
                {
                    _vm.IsAbortConfirmVisible = false;
                    e.Handled = true;
                }

                return;
            }

            if (e.KeyModifiers == KeyModifiers.Alt)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        _vm.KeepCurrent();
                        e.Handled = true;
                        break;
                    case Key.Right:
                        _vm.KeepIncoming();
                        e.Handled = true;
                        break;
                    case Key.Down:
                        _vm.KeepBoth();
                        e.Handled = true;
                        break;
                }
            }
            else if (e.KeyModifiers == cmd)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        _vm.GotoNextConflict();
                        e.Handled = true;
                        break;
                    case Key.Up:
                        _vm.GotoPrevConflict();
                        e.Handled = true;
                        break;
                    case Key.Enter:
                        _ = _vm.FinishAsync();
                        e.Handled = true;
                        break;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Escape)
            {
                _vm.Close();
                e.Handled = true;
            }
        }

        private void OnBackToHistory(object sender, RoutedEventArgs e)
        {
            _vm?.Close();
            e.Handled = true;
        }

        private void OnAbort(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.IsAbortConfirmVisible = true;
            e.Handled = true;
        }

        private void OnCancelAbort(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.IsAbortConfirmVisible = false;
            Focus();
            e.Handled = true;
        }

        private async void OnConfirmAbort(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_vm != null)
                await _vm.AbortAsync();
        }

        private async void OnFinish(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_vm != null)
                await _vm.FinishAsync();
        }

        private async void OnMarkResolved(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_vm != null)
                await _vm.MarkResolvedAsync();
            Focus();
        }

        private void OnPrevConflict(object sender, RoutedEventArgs e)
        {
            _vm?.GotoPrevConflict();
            Focus();
            e.Handled = true;
        }

        private void OnNextConflict(object sender, RoutedEventArgs e)
        {
            _vm?.GotoNextConflict();
            Focus();
            e.Handled = true;
        }

        private void OnFileMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                CreateWholeFileMenu()?.Open(control);
            e.Handled = true;
        }

        private void OnFileContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (_vm?.SelectedFile is { IsResolved: false })
                CreateWholeFileMenu()?.Open(FilesList);
            e.Handled = true;
        }

        private ContextMenu CreateWholeFileMenu()
        {
            if (_vm?.Detail == null)
                return null;

            var useCurrent = new MenuItem();
            useCurrent.Header = App.Text("ConflictResolver.WholeFile.Current", _vm.Current.Name);
            useCurrent.Icon = this.CreateMenuIcon("Icons.Local");
            useCurrent.Click += async (_, ev) =>
            {
                ev.Handled = true;
                await _vm.UseWholeFileAsync(false);
            };

            var useIncoming = new MenuItem();
            useIncoming.Header = App.Text("ConflictResolver.WholeFile.Incoming", _vm.Incoming.Name);
            useIncoming.Icon = this.CreateMenuIcon("Icons.Incoming");
            useIncoming.Click += async (_, ev) =>
            {
                ev.Handled = true;
                await _vm.UseWholeFileAsync(true);
            };

            var external = new MenuItem();
            external.Header = App.Text("WorkingCopy.Conflicts.MergeExternal");
            external.Icon = this.CreateMenuIcon("Icons.OpenWith");
            external.IsEnabled = _vm.Editor != null;
            external.Click += async (_, ev) =>
            {
                ev.Handled = true;
                await _vm.OpenExternalMergeToolAsync();
            };

            var menu = new ContextMenu();
            menu.Items.Add(useCurrent);
            menu.Items.Add(useIncoming);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(external);
            return menu;
        }

        private ViewModels.ConflictResolver _vm = null;
        private ViewModels.MergeConflictEditor _editor = null;
        private bool _syncingScroll = false;
    }
}
