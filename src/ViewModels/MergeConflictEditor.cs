using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class MergeConflictEditor : ObservableObject
    {
        public string FilePath
        {
            get => _filePath;
        }

        public object Mine
        {
            get;
        }

        public object Theirs
        {
            get;
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        public List<Models.ConflictLine> OursLines
        {
            get => _oursLines;
            private set => SetProperty(ref _oursLines, value);
        }

        public List<Models.ConflictLine> TheirsLines
        {
            get => _theirsLines;
            private set => SetProperty(ref _theirsLines, value);
        }

        public List<Models.ConflictLine> ResultLines
        {
            get => _resultLines;
            private set => SetProperty(ref _resultLines, value);
        }

        public int MaxLineNumber
        {
            get => _maxLineNumber;
            private set => SetProperty(ref _maxLineNumber, value);
        }

        public int UnsolvedCount
        {
            get => _unsolvedCount;
            private set => SetProperty(ref _unsolvedCount, value);
        }

        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetProperty(ref _selectedChunk, value);
        }

        public IReadOnlyList<Models.ConflictRegion> ConflictRegions
        {
            get => _conflictRegions;
        }

        /// <summary>
        ///     Rows of the Current / Incoming / Result panes of the conflict resolver screen.
        /// </summary>
        public List<Models.ConflictPaneRow> CurrentRows
        {
            get => _currentRows;
            private set => SetProperty(ref _currentRows, value);
        }

        public List<Models.ConflictPaneRow> IncomingRows
        {
            get => _incomingRows;
            private set => SetProperty(ref _incomingRows, value);
        }

        public List<Models.ConflictPaneRow> ResultRows
        {
            get => _resultRows;
            private set => SetProperty(ref _resultRows, value);
        }

        /// <summary>
        ///     The block the keyboard shortcuts apply to (keep current/incoming/both, next/previous conflict).
        /// </summary>
        public int CurrentRegion
        {
            get => _currentRegion;
            set
            {
                if (SetProperty(ref _currentRegion, value))
                    OnPropertyChanged(nameof(CurrentRegionLabel));
            }
        }

        public string CurrentRegionLabel
        {
            get => App.Text("ConflictResolver.Counter", Math.Max(1, _currentRegion + 1), _conflictRegions.Count);
        }

        public MergeConflictEditor(Repository repo, Models.Commit head, string filePath)
        {
            _repo = repo;
            _filePath = filePath;

            (Mine, Theirs) = repo.InProgressContext switch
            {
                CherryPickInProgress cherryPick => (head, cherryPick.Head),
                RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                RevertInProgress revert => (head, revert.Head),
                MergeInProgress merge => (head, merge.Source),
                _ => (head, (object)"Stash or Patch"),
            };

            var workingCopyPath = Path.Combine(_repo.FullPath, _filePath);
            var workingCopyContent = string.Empty;
            if (File.Exists(workingCopyPath))
                workingCopyContent = File.ReadAllText(workingCopyPath);

            if (workingCopyContent.IndexOf('\0', StringComparison.Ordinal) >= 0)
            {
                _error = "Binary file is not supported.";
                return;
            }

            ParseOriginalContent(workingCopyContent);
            RefreshDisplayData();
        }

        public Models.ConflictLineState GetLineState(int line)
        {
            if (line >= 0 && line < _lineStates.Count)
                return _lineStates[line];
            return Models.ConflictLineState.Normal;
        }

        public void Resolve(object param)
        {
            if (_selectedChunk == null)
                return;

            var region = _conflictRegions[_selectedChunk.ConflictIndex];
            if (param is not Models.ConflictResolution resolution)
                return;

            // Try to resolve a resolved region.
            if (resolution != Models.ConflictResolution.None && region.IsResolved)
                return;

            // Try to undo an unresolved region.
            if (resolution == Models.ConflictResolution.None && !region.IsResolved)
                return;

            region.IsResolved = resolution != Models.ConflictResolution.None;
            region.ResolutionType = resolution;
            RefreshDisplayData();
        }

        public void ResolveRegion(int index, Models.ConflictResolution resolution)
        {
            if (index < 0 || index >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[index];
            region.IsResolved = resolution != Models.ConflictResolution.None;
            region.ResolutionType = resolution;
            RefreshDisplayData();

            if (!region.IsResolved)
            {
                CurrentRegion = index;
                return;
            }

            // Move on to the next unresolved block, so that the shortcuts can be chained.
            for (var i = 1; i <= _conflictRegions.Count; i++)
            {
                var next = (index + i) % _conflictRegions.Count;
                if (!_conflictRegions[next].IsResolved)
                {
                    CurrentRegion = next;
                    return;
                }
            }
        }

        /// <summary>
        ///     Keep / un-keep one side of a block. Keeping the other side of a block that already keeps one makes it keep
        ///     both (in the order they were picked); un-keeping one side of "both" keeps the other one.
        /// </summary>
        public void ToggleSide(int index, bool current)
        {
            if (index < 0 || index >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[index];
            var resolution = region.IsResolved ? region.ResolutionType : Models.ConflictResolution.None;
            var both = resolution is Models.ConflictResolution.UseBothMineFirst or Models.ConflictResolution.UseBothTheirsFirst;
            var keepsCurrent = both || resolution == Models.ConflictResolution.UseOurs;
            var keepsIncoming = both || resolution == Models.ConflictResolution.UseTheirs;

            Models.ConflictResolution next;
            if (current)
            {
                if (keepsCurrent)
                    next = both ? Models.ConflictResolution.UseTheirs : Models.ConflictResolution.None;
                else
                    next = keepsIncoming ? Models.ConflictResolution.UseBothTheirsFirst : Models.ConflictResolution.UseOurs;
            }
            else
            {
                if (keepsIncoming)
                    next = both ? Models.ConflictResolution.UseOurs : Models.ConflictResolution.None;
                else
                    next = keepsCurrent ? Models.ConflictResolution.UseBothMineFirst : Models.ConflictResolution.UseTheirs;
            }

            ResolveRegion(index, next);
        }

        public void ToggleCurrentRegionSide(bool current)
        {
            ToggleSide(_currentRegion, current);
        }

        public void ResolveCurrentRegion(Models.ConflictResolution resolution)
        {
            ResolveRegion(_currentRegion, resolution);
        }

        public void GotoRegion(int delta)
        {
            if (_conflictRegions.Count > 0)
                CurrentRegion = Math.Clamp(_currentRegion + delta, 0, _conflictRegions.Count - 1);
        }

        public async Task<bool> SaveAndStageAsync()
        {
            if (_conflictRegions.Count == 0)
                return true;

            if (_unsolvedCount > 0)
            {
                Error = "Cannot save: there are still unresolved conflicts.";
                return false;
            }

            var lines = _originalContent.Split('\n', StringSplitOptions.None);
            var builder = new StringBuilder();
            var lastLineIdx = 0;

            foreach (var r in _conflictRegions)
            {
                for (var i = lastLineIdx; i < r.StartLineInOriginal; i++)
                    builder.Append(lines[i]).Append('\n');

                if (r.ResolutionType == Models.ConflictResolution.UseOurs)
                {
                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseTheirs)
                {
                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseBothMineFirst)
                {
                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');

                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseBothTheirsFirst)
                {
                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');

                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');
                }

                lastLineIdx = r.EndLineInOriginal + 1;
            }

            for (var j = lastLineIdx; j < lines.Length; j++)
                builder.Append(lines[j]).Append('\n');

            if (builder.Length > 1)
                builder.Length--; // Remove extra '\n'

            try
            {
                // Write merged content to file
                var fullPath = Path.Combine(_repo.FullPath, _filePath);
                await File.WriteAllTextAsync(fullPath, builder.ToString());

                // Stage the file
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(pathSpecFile, _filePath);
                await new Commands.Add(_repo.FullPath, pathSpecFile).ExecAsync();
                File.Delete(pathSpecFile);

                _repo.MarkWorkingCopyDirtyManually();
                return true;
            }
            catch (Exception ex)
            {
                Error = $"Failed to save and stage: {ex.Message}";
                return false;
            }
        }

        public void ClearErrorMessage()
        {
            Error = string.Empty;
        }

        private void ParseOriginalContent(string content)
        {
            _originalContent = content;
            _conflictRegions.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            var lines = content.Split('\n', StringSplitOptions.None);
            _originalLines = lines;
            var oursLines = new List<Models.ConflictLine>();
            var theirsLines = new List<Models.ConflictLine>();
            int oursLineNumber = 1;
            int theirsLineNumber = 1;
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    var region = new Models.ConflictRegion
                    {
                        StartLineInOriginal = i,
                        StartMarker = line,
                    };

                    oursLines.Add(new());
                    theirsLines.Add(new());
                    i++;

                    // Collect ours content
                    while (i < lines.Length &&
                           !lines[i].StartsWith("|||||||", StringComparison.Ordinal) &&
                           !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        line = lines[i];
                        region.OursContent.Add(line);
                        oursLines.Add(new(Models.ConflictLineType.Ours, line, oursLineNumber++));
                        theirsLines.Add(new());
                        i++;
                    }

                    // Skip diff3 base section if present
                    if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                    {
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                            i++;
                    }

                    // Capture separator marker
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        oursLines.Add(new());
                        theirsLines.Add(new());
                        region.SeparatorMarker = lines[i];
                        i++;
                    }

                    // Collect theirs content
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        line = lines[i];
                        region.TheirsContent.Add(line);
                        oursLines.Add(new());
                        theirsLines.Add(new(Models.ConflictLineType.Theirs, line, theirsLineNumber++));
                        i++;
                    }

                    // Capture end marker (e.g., ">>>>>>> feature-branch")
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        oursLines.Add(new());
                        theirsLines.Add(new());

                        region.EndMarker = lines[i];
                        region.EndLineInOriginal = i;
                        i++;
                    }

                    _conflictRegions.Add(region);
                }
                else
                {
                    oursLines.Add(new(Models.ConflictLineType.Common, line, oursLineNumber));
                    theirsLines.Add(new(Models.ConflictLineType.Common, line, theirsLineNumber));
                    i++;
                    oursLineNumber++;
                    theirsLineNumber++;
                }
            }

            MaxLineNumber = Math.Max(oursLineNumber, theirsLineNumber);
            OursLines = oursLines;
            TheirsLines = theirsLines;
        }

        private void RefreshDisplayData()
        {
            var resultLines = new List<Models.ConflictLine>();
            _lineStates.Clear();

            if (_oursLines == null || _oursLines.Count == 0)
            {
                ResultLines = resultLines;
                return;
            }

            int resultLineNumber = 1;
            int currentLine = 0;
            int conflictIdx = 0;

            while (currentLine < _oursLines.Count)
            {
                // Check if we're at a conflict region
                Models.ConflictRegion currentRegion = null;
                if (conflictIdx < _conflictRegions.Count)
                {
                    var region = _conflictRegions[conflictIdx];
                    if (region.StartLineInOriginal == currentLine)
                        currentRegion = region;
                }

                if (currentRegion != null)
                {
                    int regionLines = currentRegion.EndLineInOriginal - currentRegion.StartLineInOriginal + 1;
                    if (currentRegion.IsResolved)
                    {
                        var oldLineCount = resultLines.Count;
                        var resolveType = currentRegion.ResolutionType;

                        // Resolved - show resolved content with color based on resolution type
                        if (resolveType == Models.ConflictResolution.UseBothMineFirst)
                        {
                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }

                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseBothTheirsFirst)
                        {
                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }

                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseOurs)
                        {
                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseTheirs)
                        {
                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }

                        // Pad with empty lines to match Mine/Theirs panel height
                        int added = resultLines.Count - oldLineCount;
                        int padding = regionLines - added;
                        for (int p = 0; p < padding; p++)
                            resultLines.Add(new());

                        int blockSize = resultLines.Count - oldLineCount - 2;
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockStart);
                        for (var i = 0; i < blockSize; i++)
                            _lineStates.Add(Models.ConflictLineState.ResolvedBlock);
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockEnd);
                    }
                    else
                    {
                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.StartMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockStart);

                        foreach (var line in currentRegion.OursContent)
                        {
                            resultLines.Add(new(Models.ConflictLineType.Ours, line, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.SeparatorMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlock);

                        foreach (var line in currentRegion.TheirsContent)
                        {
                            resultLines.Add(new(Models.ConflictLineType.Theirs, line, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.EndMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockEnd);
                    }

                    currentLine = currentRegion.EndLineInOriginal + 1;
                    conflictIdx++;
                }
                else
                {
                    var oursLine = _oursLines[currentLine];
                    resultLines.Add(new(oursLine.Type, oursLine.Content, resultLineNumber));
                    _lineStates.Add(Models.ConflictLineState.Normal);
                    resultLineNumber++;
                    currentLine++;
                }
            }

            SelectedChunk = null;
            ResultLines = resultLines;

            var unsolved = new List<int>();
            for (var i = 0; i < _conflictRegions.Count; i++)
            {
                var r = _conflictRegions[i];
                if (!r.IsResolved)
                    unsolved.Add(i);
            }

            UnsolvedCount = unsolved.Count;
            BuildPaneRows();
        }

        private void BuildPaneRows()
        {
            var current = new List<Models.ConflictPaneRow>();
            var incoming = new List<Models.ConflictPaneRow>();
            var result = new List<Models.ConflictPaneRow>();
            var lines = _originalLines;
            var curNo = 1;
            var incNo = 1;
            var resNo = 1;
            var regionIdx = 0;
            var idx = 0;

            while (idx < lines.Length)
            {
                var region = regionIdx < _conflictRegions.Count && _conflictRegions[regionIdx].StartLineInOriginal == idx ? _conflictRegions[regionIdx] : null;
                if (region == null)
                {
                    // The empty string after the final '\n' is not a line.
                    if (idx == lines.Length - 1 && lines[idx].Length == 0)
                        break;

                    var text = lines[idx].TrimEnd('\r');
                    current.Add(new() { LineNumber = (curNo++).ToString(), Content = text });
                    incoming.Add(new() { LineNumber = (incNo++).ToString(), Content = text });
                    result.Add(new() { LineNumber = (resNo++).ToString(), Content = text });
                    idx++;
                    continue;
                }

                var resolution = region.ResolutionType;
                var keepCurrent = region.IsResolved && resolution != Models.ConflictResolution.UseTheirs;
                var keepIncoming = region.IsResolved && resolution != Models.ConflictResolution.UseOurs;
                var label = GetRegionLabel(regionIdx);

                current.Add(new() { Kind = Models.ConflictPaneRowKind.Header, Side = Models.ConflictPaneSide.Current, Region = regionIdx, Label = label, IsKept = keepCurrent, IsDropped = region.IsResolved && !keepCurrent, Resolution = resolution });
                incoming.Add(new() { Kind = Models.ConflictPaneRowKind.Header, Side = Models.ConflictPaneSide.Incoming, Region = regionIdx, Label = label, IsKept = keepIncoming, IsDropped = region.IsResolved && !keepIncoming, Resolution = resolution });

                var height = Math.Max(region.OursContent.Count, region.TheirsContent.Count);
                for (var i = 0; i < height; i++)
                {
                    current.Add(i < region.OursContent.Count
                        ? new() { Side = Models.ConflictPaneSide.Current, Region = regionIdx, LineNumber = (curNo++).ToString(), Content = region.OursContent[i].TrimEnd('\r'), IsDropped = region.IsResolved && !keepCurrent }
                        : new() { Kind = Models.ConflictPaneRowKind.Padding, Region = regionIdx });
                    incoming.Add(i < region.TheirsContent.Count
                        ? new() { Side = Models.ConflictPaneSide.Incoming, Region = regionIdx, LineNumber = (incNo++).ToString(), Content = region.TheirsContent[i].TrimEnd('\r'), IsDropped = region.IsResolved && !keepIncoming }
                        : new() { Kind = Models.ConflictPaneRowKind.Padding, Region = regionIdx });
                }

                if (!region.IsResolved)
                {
                    result.Add(new() { Kind = Models.ConflictPaneRowKind.Placeholder, Region = regionIdx, Label = label });
                }
                else
                {
                    var kept = new List<(Models.ConflictPaneSide, string)>();
                    void AddSide(Models.ConflictPaneSide side, List<string> content)
                    {
                        foreach (var l in content)
                            kept.Add((side, l.TrimEnd('\r')));
                    }

                    switch (resolution)
                    {
                        case Models.ConflictResolution.UseOurs:
                            AddSide(Models.ConflictPaneSide.Current, region.OursContent);
                            break;
                        case Models.ConflictResolution.UseTheirs:
                            AddSide(Models.ConflictPaneSide.Incoming, region.TheirsContent);
                            break;
                        case Models.ConflictResolution.UseBothMineFirst:
                            AddSide(Models.ConflictPaneSide.Current, region.OursContent);
                            AddSide(Models.ConflictPaneSide.Incoming, region.TheirsContent);
                            break;
                        case Models.ConflictResolution.UseBothTheirsFirst:
                            AddSide(Models.ConflictPaneSide.Incoming, region.TheirsContent);
                            AddSide(Models.ConflictPaneSide.Current, region.OursContent);
                            break;
                    }

                    // An empty side still gets a row, which carries the origin tag and its Undo.
                    if (kept.Count == 0)
                        kept.Add((resolution == Models.ConflictResolution.UseTheirs ? Models.ConflictPaneSide.Incoming : Models.ConflictPaneSide.Current, null));

                    for (var i = 0; i < kept.Count; i++)
                    {
                        var (side, text) = kept[i];
                        result.Add(new()
                        {
                            Side = side,
                            Region = regionIdx,
                            LineNumber = text == null ? string.Empty : (resNo++).ToString(),
                            Content = text ?? string.Empty,
                            IsKept = i == 0,
                            Resolution = resolution,
                        });
                    }
                }

                idx = region.EndLineInOriginal + 1;
                regionIdx++;
            }

            CurrentRows = current;
            IncomingRows = incoming;
            ResultRows = result;
        }

        /// <summary>
        ///     Names a block after the closest meaningful line above it (like the function context of a diff hunk).
        /// </summary>
        private string GetRegionLabel(int regionIdx)
        {
            var start = _conflictRegions[regionIdx].StartLineInOriginal;
            var stop = regionIdx > 0 ? _conflictRegions[regionIdx - 1].EndLineInOriginal : -1;
            for (var i = start - 1; i > stop; i--)
            {
                var text = _originalLines[i].Trim();
                if (text.Length < 3 || text.Trim('{', '}', '(', ')', '[', ']', ';', ',', ' ').Length == 0)
                    continue;

                return text.Length > 48 ? string.Concat(text.AsSpan(0, 47), "…") : text;
            }

            return string.Empty;
        }

        private readonly Repository _repo;
        private readonly string _filePath;
        private string _originalContent = string.Empty;
        private int _unsolvedCount = 0;
        private int _maxLineNumber = 0;
        private List<Models.ConflictLine> _oursLines = [];
        private List<Models.ConflictLine> _theirsLines = [];
        private List<Models.ConflictLine> _resultLines = [];
        private List<Models.ConflictRegion> _conflictRegions = [];
        private List<Models.ConflictLineState> _lineStates = [];
        private string[] _originalLines = [];
        private List<Models.ConflictPaneRow> _currentRows = [];
        private List<Models.ConflictPaneRow> _incomingRows = [];
        private List<Models.ConflictPaneRow> _resultRows = [];
        private int _currentRegion = 0;
        private Vector _scrollOffset = Vector.Zero;
        private Models.ConflictSelectedChunk _selectedChunk;
        private string _error = string.Empty;
    }
}
