using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    /// <summary>
    ///     Right-hand inspector shown when the "uncommitted changes" row of the graph is selected:
    ///     stage/unstage files, see their diff in the center, and commit without leaving the history.
    ///     The commit shortcuts (⌘/Ctrl+Enter…) come from the HotKeys of the WorkingCopy page, which share this view model.
    /// </summary>
    public partial class WorkingCopyInspector : UserControl
    {
        public WorkingCopyInspector()
        {
            InitializeComponent();
        }

        private void OnOpenStagingView(object sender, RoutedEventArgs e)
        {
            if (this.FindAncestorOfType<Histories>()?.DataContext is ViewModels.Histories histories)
                histories.OpenLocalChanges();

            e.Handled = true;
        }

        private void OnTreeMode(object sender, RoutedEventArgs e)
        {
            // Shared with the staging view: both show the uncommitted files the same way.
            ViewModels.Preferences.Instance.ChangeViewMode = Models.ChangeViewMode.Tree;
            e.Handled = true;
        }

        private void OnListMode(object sender, RoutedEventArgs e)
        {
            ViewModels.Preferences.Instance.ChangeViewMode = Models.ChangeViewMode.List;
            e.Handled = true;
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { Repository: { } repo, SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var menu = WorkingCopy.CreateContextMenuForUnstagedChanges(this, UnstagedChangesView, repo, vm, selection);
                menu?.Open(sender as Control);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { Repository: { } repo, SelectedStaged: { Count: > 0 } selection } vm)
            {
                var menu = WorkingCopy.CreateContextMenuForStagedChanges(this, StagedChangesView, repo, vm, selection);
                menu?.Open(sender as Control);
                e.Handled = true;
            }
        }

        private async void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { SelectedUnstaged: { Count: > 0 } selection } vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(selection.Changes, next);
                UnstagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy { SelectedStaged: { Count: > 0 } selection } vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(selection.Changes, next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnUnstagedKeyDown(object _, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.None ||
                DataContext is not ViewModels.WorkingCopy { SelectedUnstaged: { Count: > 0 } selection } vm)
                return;

            if (e.Key is Key.Space or Key.Enter)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                await vm.StageChangesAsync(selection.Changes, next);
                UnstagedChangesView.TakeFocus();
                e.Handled = true;
            }
            else if (e.Key is Key.Delete or Key.Back)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.Discard(selection.Changes, next);
                e.Handled = true;
            }
        }

        private async void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.None ||
                DataContext is not ViewModels.WorkingCopy { SelectedStaged: { Count: > 0 } selection } vm)
                return;

            if (e.Key is Key.Space or Key.Enter)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                await vm.UnstageChangesAsync(selection.Changes, next);
                StagedChangesView.TakeFocus();
                e.Handled = true;
            }
        }

        private async void OnStageRow(object _, ChangeRowActionEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.StageChangesAsync(e.Changes, null);

            e.Handled = true;
        }

        private async void OnUnstageRow(object _, ChangeRowActionEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.UnstageChangesAsync(e.Changes, null);

            e.Handled = true;
        }

        private async void OnStageAll(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.StageChangesAsync(vm.VisibleUnstaged, null);

            e.Handled = true;
        }

        private async void OnUnstageAll(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.UnstageChangesAsync(vm.VisibleStaged, null);

            e.Handled = true;
        }

        private async void OnCommit(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, false);

            e.Handled = true;
        }

        private async void OnCommitWithPush(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.CommitAsync(false, true);

            e.Handled = true;
        }

        private async void OnContinue(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm)
                await vm.ContinueMergeAsync();

            e.Handled = true;
        }
    }
}
