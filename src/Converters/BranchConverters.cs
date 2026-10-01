using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class BranchConverters
    {
        public static readonly FuncValueConverter<string, IBrush> ToLaneBrush =
            new FuncValueConverter<string, IBrush>(Models.BranchColor.BrushOf);

        public static readonly FuncValueConverter<Models.Branch, IBrush> BranchToLaneBrush =
            new FuncValueConverter<Models.Branch, IBrush>(v => Models.BranchColor.BrushOf(v?.Name));
    }
}
