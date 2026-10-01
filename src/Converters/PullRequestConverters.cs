using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class PullRequestConverters
    {
        public static readonly FuncValueConverter<Models.PullRequestState, IBrush> ToStateBrush =
            new FuncValueConverter<Models.PullRequestState, IBrush>(GetStateBrush);

        public static readonly FuncValueConverter<Models.PullRequestState, IBrush> ToStateBackground =
            new FuncValueConverter<Models.PullRequestState, IBrush>(v =>
            {
                if (GetStateBrush(v) is ISolidColorBrush solid)
                    return new SolidColorBrush(solid.Color, 0.14);

                return Brushes.Transparent;
            });

        private static IBrush GetStateBrush(Models.PullRequestState state)
        {
            var key = state switch
            {
                Models.PullRequestState.Merged => "Brush.Added",
                Models.PullRequestState.Closed => "Brush.Deleted",
                Models.PullRequestState.Draft => "Brush.FG3",
                _ => "Brush.Warning",
            };

            return App.Current?.FindResource(key) as IBrush ?? Brushes.Gray;
        }
    }
}
