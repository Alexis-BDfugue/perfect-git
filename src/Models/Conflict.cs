using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum ConflictFileState
    {
        Unknown,
        UnmergedText,
        UnmergedBinary,
        Resolved
    }

    public enum ConflictPanelType
    {
        Ours,
        Theirs,
        Result
    }

    public enum ConflictResolution
    {
        None,
        UseOurs,
        UseTheirs,
        UseBothMineFirst,
        UseBothTheirsFirst,
    }

    public enum ConflictLineType
    {
        None,
        Common,
        Marker,
        Ours,
        Theirs,
    }

    public enum ConflictLineState
    {
        Normal,
        ConflictBlockStart,
        ConflictBlock,
        ConflictBlockEnd,
        ResolvedBlockStart,
        ResolvedBlock,
        ResolvedBlockEnd,
    }

    public class ConflictLine
    {
        public ConflictLineType Type { get; set; } = ConflictLineType.None;
        public string Content { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;

        public ConflictLine()
        {
        }
        public ConflictLine(ConflictLineType type, string content)
        {
            Type = type;
            Content = content;
        }
        public ConflictLine(ConflictLineType type, string content, int lineNumber)
        {
            Type = type;
            Content = content;
            LineNumber = lineNumber.ToString();
        }
    }

    public record ConflictSelectedChunk(
        double Y,
        double Height,
        int ConflictIndex,
        ConflictPanelType Panel,
        bool IsResolved
    );

    public class ConflictRegion
    {
        public int StartLineInOriginal { get; set; }
        public int EndLineInOriginal { get; set; }

        public string StartMarker { get; set; } = "<<<<<<<";
        public string SeparatorMarker { get; set; } = "=======";
        public string EndMarker { get; set; } = ">>>>>>>";

        public List<string> OursContent { get; set; } = new();
        public List<string> TheirsContent { get; set; } = new();

        public bool IsResolved { get; set; } = false;
        public ConflictResolution ResolutionType { get; set; } = ConflictResolution.None;
    }

    public enum ConflictPaneRowKind
    {
        /// <summary>A line of the file (context, or one side of a conflict).</summary>
        Line,
        /// <summary>"Conflict n" header of a block, with its Keep button (Current/Incoming panes).</summary>
        Header,
        /// <summary>Empty row keeping both sides of a block aligned (Current/Incoming panes).</summary>
        Padding,
        /// <summary>Unresolved block in the Result pane.</summary>
        Placeholder,
    }

    public enum ConflictPaneSide
    {
        None,
        Current,
        Incoming,
    }

    /// <summary>
    ///     One row of the conflict resolver panes (Current, Incoming or Result).
    /// </summary>
    public class ConflictPaneRow
    {
        public ConflictPaneRowKind Kind { get; init; } = ConflictPaneRowKind.Line;
        public ConflictPaneSide Side { get; init; } = ConflictPaneSide.None;
        public int Region { get; init; } = -1;
        public string LineNumber { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;

        /// <summary>Line of a side that the resolution of its block doesn't keep.</summary>
        public bool IsDropped { get; init; } = false;

        /// <summary>Header: this side is kept. Result: first line of a resolved block (shows its origin and Undo).</summary>
        public bool IsKept { get; init; } = false;

        public ConflictResolution Resolution { get; init; } = ConflictResolution.None;
    }
}
