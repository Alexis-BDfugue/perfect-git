using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    /// <summary>
    ///     Right-hand inspector for a pull request selected in the sidebar (see ViewModels.PullRequestDetail).
    /// </summary>
    public partial class PullRequestDetail : UserControl
    {
        public PullRequestDetail()
        {
            InitializeComponent();
        }

        private void OnShowOverview(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail vm)
                vm.ShowOverview();
            e.Handled = true;
        }

        private void OnTreeMode(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail vm)
                vm.IsInspectorTreeMode = true;
            e.Handled = true;
        }

        private void OnListMode(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail vm)
                vm.IsInspectorTreeMode = false;
            e.Handled = true;
        }

        private void OnRowTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail detail && sender is Control { DataContext: ViewModels.CommitInspectorRow { IsFolder: true } folder })
            {
                detail.ToggleFolder(folder.FolderPath);
                e.Handled = true;
                return;
            }

            if (DataContext is ViewModels.PullRequestDetail vm && sender is Control { DataContext: ViewModels.CommitInspectorRow { Change: { } change } })
            {
                vm.OpenDiffInCenter(change);
                e.Handled = true;
            }
        }

        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Keyboard navigation in the files list follows along while a diff is open.
            if (DataContext is ViewModels.PullRequestDetail { CenterDiff: not null } vm &&
                sender is ListBox { SelectedItem: ViewModels.CommitInspectorRow { Change: { } change } })
                vm.OpenDiffInCenter(change);
        }

        private async void OnCheckout(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail vm)
                await vm.CheckoutAsync();
            e.Handled = true;
        }

        private void OnOpenInBrowser(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestDetail vm)
                vm.OpenInBrowser();
            e.Handled = true;
        }
    }
}
