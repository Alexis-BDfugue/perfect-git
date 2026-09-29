using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class QuickPalette : UserControl
    {
        public QuickPalette()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            FilterTextBox.Focus(NavigationMethod.Directional);
            FilterTextBox.CaretIndex = FilterTextBox.Text?.Length ?? 0;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.QuickPalette vm)
            {
                vm.PropertyChanged += (_, ev) =>
                {
                    if (ev.PropertyName == nameof(ViewModels.QuickPalette.Selected) && vm.Selected != null)
                        ItemsList.ScrollIntoView(vm.Selected);
                    else if (ev.PropertyName == nameof(ViewModels.QuickPalette.Filter))
                        FilterTextBox.CaretIndex = vm.Filter?.Length ?? 0;
                };
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.QuickPalette vm)
                return;

            var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            switch (e.Key)
            {
                case Key.Down:
                    vm.SelectNext(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    vm.SelectNext(-1);
                    e.Handled = true;
                    break;
                case Key.Enter when e.KeyModifiers.HasFlag(cmdKey):
                    if (vm.TakeGitCommand() is { } cmd)
                        await this.CopyTextAsync(cmd);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    vm.Exec();
                    e.Handled = true;
                    break;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.QuickPalette vm && sender is Control { DataContext: ViewModels.QuickPaletteItem item })
            {
                vm.Exec(item);
                e.Handled = true;
            }
        }
    }
}
