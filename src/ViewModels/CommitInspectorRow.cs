using Avalonia;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     One line of the changed files list in the commit inspector: a folder header or a file.
    /// </summary>
    public class CommitInspectorRow
    {
        public bool IsFolder { get; init; } = false;
        public string Label { get; init; } = string.Empty;
        public Models.Change Change { get; init; } = null;
        public Thickness Indent { get; init; } = new(0);
        public string Added { get; init; } = string.Empty;
        public string Deleted { get; init; } = string.Empty;
    }
}
