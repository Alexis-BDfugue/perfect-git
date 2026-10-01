using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ChangeViewModeConverters
    {
        public static readonly FuncValueConverter<Models.ChangeViewMode, bool> IsTree =
            new FuncValueConverter<Models.ChangeViewMode, bool>(v => v == Models.ChangeViewMode.Tree);

        public static readonly FuncValueConverter<Models.ChangeViewMode, bool> IsNotTree =
            new FuncValueConverter<Models.ChangeViewMode, bool>(v => v != Models.ChangeViewMode.Tree);
    }
}
