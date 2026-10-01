using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class BranchDropPopover : UserControl
    {
        public Action CloseRequested { get; set; }

        /// <summary>
        ///     Control the popover is anchored to (used to open dialogs on the main window).
        /// </summary>
        public Control Anchor { get; set; }

        public BranchDropPopover()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            OptionsList.Focus(NavigationMethod.Directional);
        }

        private async void OnOptionsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.BranchDrop vm)
            {
                e.Handled = true;
                await RunAsync(vm, vm.SelectedOption);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseRequested?.Invoke();
            }
        }

        private async void OnOptionTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.BranchDropOption option } && DataContext is ViewModels.BranchDrop vm)
            {
                e.Handled = true;
                await RunAsync(vm, option);
            }
        }

        private async Task RunAsync(ViewModels.BranchDrop vm, ViewModels.BranchDropOption option)
        {
            if (option is not { IsEnabled: true })
                return;

            var anchor = Anchor;
            CloseRequested?.Invoke();

            await vm.ExecuteAsync(option, async on =>
            {
                if (anchor != null)
                    await anchor.ShowDialogAsync(new ViewModels.InteractiveRebase(vm.Repository, on));
            });
        }
    }
}
