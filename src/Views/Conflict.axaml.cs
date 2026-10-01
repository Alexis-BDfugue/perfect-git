using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Conflict : UserControl
    {
        public Conflict()
        {
            InitializeComponent();
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo } && sender is TextBlock text)
                repo.NavigateToCommit(text.Text);

            e.Handled = true;
        }

        private async void OnUseTheirs(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseTheirsAsync();

            e.Handled = true;
        }

        private async void OnUseMine(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseMineAsync();

            e.Handled = true;
        }

        private void OnMerge(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
            {
                var request = vm.CreateOpenMergeEditorRequest();
                this.ShowWindow(request);
            }

            e.Handled = true;
        }

        private void OnGotoPrevConflict(object _, RoutedEventArgs e)
        {
            Resolver.GotoPrevConflict();
            e.Handled = true;
        }

        private void OnGotoNextConflict(object _, RoutedEventArgs e)
        {
            Resolver.GotoNextConflict();
            e.Handled = true;
        }

        private async void OnSaveAndStage(object _, RoutedEventArgs e)
        {
            // Once staged, the file leaves the conflict list and the working copy selects the next conflict.
            if (DataContext is ViewModels.Conflict vm && IsEffectivelyVisible)
                await vm.SaveAndStageAsync();

            e.Handled = true;
        }

        private async void OnMergeExternal(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.MergeExternalAsync();

            e.Handled = true;
        }
    }
}
