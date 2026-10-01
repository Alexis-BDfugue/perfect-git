using Avalonia;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class PaletteConverters
    {
        public static readonly FuncValueConverter<object, double> ToWidth =
            new FuncValueConverter<object, double>(v => v is ViewModels.QuickPalette ? 640 : 460);

        public static readonly FuncValueConverter<object, Thickness> ToPadding =
            new FuncValueConverter<object, Thickness>(v => v is ViewModels.QuickPalette ? new Thickness(0) : new Thickness(16, 10, 16, 12));
    }
}
