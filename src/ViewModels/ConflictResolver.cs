using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     A file that has been in conflict during the operation in progress.
    /// </summary>
    public class ConflictFile : ObservableObject
    {
        public string Path { get; init; }
        public string Name { get; init; }
        public string Folder { get; init; }

        /// <summary>"src/graph/" before the file name in the editor header, empty at the root.</summary>
        public string FolderPrefix => Folder == "/" ? string.Empty : $"{Folder}/";

        /// <summary>The conflicted change, or null once the file has been resolved and staged.</summary>
        public Models.Change Change { get; init; }

        public bool IsResolved => Change == null;

        /// <summary>"✓", or "resolved/total" blocks of a text conflict.</summary>
        public string Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private string _progress = string.Empty;
    }

    /// <summary>
    ///     A side of the operation in progress: its branch (or commit) and the last commit that touched the selected file.
    /// </summary>
    public class ConflictSide : ObservableObject
    {
        public string Name { get; init; }
        public IBrush Brush { get; init; }
        public IBrush Background { get; init; }

        public Models.Commit LastCommit
        {
            get => _lastCommit;
            set
            {
                if (SetProperty(ref _lastCommit, value))
                    OnPropertyChanged(nameof(LastCommitInfo));
            }
        }

        public string LastCommitInfo
        {
            get => _lastCommit == null ? string.Empty : $"{_lastCommit.Author.Name} · {Models.RelativeTime.Format(_lastCommit.AuthorTime)}";
        }

        private Models.Commit _lastCommit = null;
    }

    /// <summary>
    ///     The "Resolve conflicts" screen, shown over the repository while a merge, rebase, cherry-pick or revert is
    ///     stopped on conflicts.
    /// </summary>
    public class ConflictResolver : ObservableObject
    {
        public Repository Repository => _repo;
        public WorkingCopy WorkingCopy => _repo.WorkingCopy;

        public string Verb { get; }
        public string Connector { get; }
        public string FinishLabel { get; }
        public string AbortLabel { get; }
        public string AbortTitle { get; }
        public string StartCommand { get; }
        public string AbortCommand { get; }
        public string UndoDescription { get; }

        public ConflictSide Current { get; }
        public ConflictSide Incoming { get; }

        public List<ConflictFile> Files
        {
            get => _files;
            private set => SetProperty(ref _files, value);
        }

        public List<string> AutoMergedFiles
        {
            get => _autoMergedFiles;
            private set => SetProperty(ref _autoMergedFiles, value);
        }

        public ConflictFile SelectedFile
        {
            get => _selectedFile;
            set
            {
                // Replacing the file list clears the list box selection for a moment: keep the current file (and its resolver).
                if (_isRefreshing && value == null)
                    return;

                if (SetProperty(ref _selectedFile, value))
                    UpdateDetail();
            }
        }

        /// <summary>
        ///     The <see cref="Conflict"/> of the selected file while it is unresolved, null once it is resolved.
        /// </summary>
        public Conflict Detail
        {
            get => _detail;
            private set
            {
                var old = _detail;
                if (SetProperty(ref _detail, value))
                {
                    if (old != null)
                        old.PropertyChanged -= OnDetailPropertyChanged;
                    if (value != null)
                        value.PropertyChanged += OnDetailPropertyChanged;

                    OnPropertyChanged(nameof(Editor));
                    OnEditorChanged();
                }
            }
        }

        public MergeConflictEditor Editor
        {
            get => _detail?.Editor;
        }

        public int ResolvedCount => _files.FindAll(x => x.IsResolved).Count;
        public int TotalCount => _files.Count;
        public bool IsAllResolved => _files.Count > 0 && ResolvedCount == _files.Count;
        public double ProgressRatio => _files.Count == 0 ? 0 : (double)ResolvedCount / _files.Count;
        public string ProgressLabel => App.Text("ConflictResolver.Progress", ResolvedCount, TotalCount);

        public string Subtitle
        {
            get => IsAllResolved
                ? App.Text("ConflictResolver.AllResolved", _opNoun)
                : App.Text("ConflictResolver.Subtitle", _autoMergedFiles.Count, TotalCount - ResolvedCount);
        }

        public string StatusText
        {
            get => IsAllResolved
                ? App.Text("ConflictResolver.Status.Ready", _opNoun)
                : App.Text("ConflictResolver.Status.Left", TotalCount - ResolvedCount);
        }

        /// <summary>"2 left" / "Resolved" tag next to the path of the selected file.</summary>
        public string FileTag
        {
            get
            {
                var editor = Editor;
                if (_selectedFile == null)
                    return string.Empty;
                if (_selectedFile.IsResolved || editor is { UnsolvedCount: 0 })
                    return App.Text("ConflictResolver.Resolved");
                return editor == null ? string.Empty : App.Text("ConflictResolver.Left", editor.UnsolvedCount);
            }
        }

        public bool IsFileTagResolved
        {
            get => _selectedFile != null && (_selectedFile.IsResolved || Editor is { UnsolvedCount: 0 });
        }

        /// <summary>Mark resolved can run: a text conflict whose blocks are all resolved.</summary>
        public bool CanMarkResolved
        {
            get => _selectedFile is { IsResolved: false } && Editor is { UnsolvedCount: 0, ConflictRegions.Count: > 0 };
        }

        public string MarkResolvedLabel
        {
            get
            {
                if (_selectedFile == null)
                    return string.Empty;
                if (_selectedFile.IsResolved)
                    return App.Text("ConflictResolver.Mark.Done", _selectedFile.Name);
                if (Editor is { UnsolvedCount: > 0 } editor)
                    return App.Text("ConflictResolver.Mark.Pending", editor.UnsolvedCount);
                return _files.FindAll(x => !x.IsResolved).Count > 1
                    ? App.Text("ConflictResolver.Mark.Next", _selectedFile.Name)
                    : App.Text("ConflictResolver.Mark", _selectedFile.Name);
            }
        }

        public string WhyDescription
        {
            get => App.Text("ConflictResolver.Why.Desc", _selectedFile?.Name ?? string.Empty);
        }

        public string ResultDescription
        {
            get => App.Text("ConflictResolver.Result.Desc", _selectedFile?.Name ?? string.Empty, _opNoun);
        }

        public string StageCommand
        {
            get => $"git add {_selectedFile?.Path}";
        }

        public bool IsAbortConfirmVisible
        {
            get => _isAbortConfirmVisible;
            set => SetProperty(ref _isAbortConfirmVisible, value);
        }

        public string AbortDescription
        {
            get
            {
                var desc = App.Text("ConflictResolver.AbortConfirm.Desc", Current.Name, Incoming.Name);
                var resolved = ResolvedCount;
                return resolved > 0 ? $"{desc} {App.Text("ConflictResolver.AbortConfirm.Lost", resolved)}" : desc;
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public ConflictResolver(Repository repo, string selectPath)
        {
            _repo = repo;

            var currentName = repo.CurrentBranch?.Name ?? "HEAD";
            string incomingName;
            switch (repo.InProgressContext)
            {
                case MergeInProgress merge:
                    _op = "Merge";
                    currentName = merge.Current;
                    incomingName = merge.SourceName;
                    _incomingRev = merge.Source?.SHA;
                    break;
                case RebaseInProgress rebase:
                    _op = "Rebase";
                    currentName = rebase.BaseName;
                    incomingName = rebase.HeadName;
                    _incomingRev = rebase.StoppedAt?.SHA;
                    break;
                case CherryPickInProgress cherryPick:
                    _op = "CherryPick";
                    incomingName = cherryPick.HeadName;
                    _incomingRev = cherryPick.Head?.SHA;
                    break;
                case RevertInProgress revert:
                    _op = "Revert";
                    incomingName = revert.Head?.SHA.Substring(0, Math.Min(10, revert.Head.SHA.Length)) ?? "REVERT_HEAD";
                    _incomingRev = revert.Head?.SHA;
                    break;
                default:
                    _op = "Merge";
                    incomingName = "MERGE_HEAD";
                    break;
            }

            _opNoun = App.Text($"ConflictResolver.Op.{_op}");
            Verb = App.Text($"ConflictResolver.{_op}.Verb");
            Connector = App.Text($"ConflictResolver.{_op}.Connector");
            FinishLabel = App.Text($"ConflictResolver.{_op}.Finish");
            AbortLabel = App.Text("ConflictResolver.Abort", _opNoun);
            AbortTitle = App.Text("ConflictResolver.AbortConfirm.Title", _opNoun);
            AbortCommand = $"git {_op.ToLowerInvariant().Replace("cherrypick", "cherry-pick")} --abort";
            StartCommand = _op switch
            {
                "Rebase" => $"git rebase {currentName}",
                "CherryPick" => $"git cherry-pick {incomingName}",
                "Revert" => $"git revert {incomingName}",
                _ => $"git merge {incomingName}",
            };
            UndoDescription = App.Text("ConflictResolver.Undo.Desc", currentName);

            // Same colors as the graph: the target branch for Current, the merged branch for Incoming.
            var currentIdx = Models.BranchColor.IndexOf(currentName);
            var incomingIdx = Models.BranchColor.IndexOf(incomingName);
            var pens = Models.CommitGraph.Pens;
            if (incomingIdx == currentIdx && pens.Count > 1)
                incomingIdx = (currentIdx + 1) % pens.Count;

            Current = CreateSide(currentName, currentIdx);
            Incoming = CreateSide(incomingName, incomingIdx);

            Refresh(selectPath);
        }

        /// <summary>
        ///     Re-reads the files from the working copy (called after each refresh of the local changes).
        /// </summary>
        public void Refresh(string selectPath = null)
        {
            var wc = _repo.WorkingCopy;
            var conflicted = new Dictionary<string, Models.Change>();
            foreach (var c in wc.Unstaged)
            {
                if (c.IsConflicted)
                    conflicted[c.Path] = c;
            }

            var paths = new List<string>(wc.ConflictedPaths);
            foreach (var p in conflicted.Keys)
            {
                if (!paths.Contains(p))
                    paths.Add(p);
            }
            paths.Sort((l, r) => Models.NumericSort.Compare(l, r));

            var files = new List<ConflictFile>();
            foreach (var p in paths)
            {
                conflicted.TryGetValue(p, out var change);
                files.Add(new ConflictFile()
                {
                    Path = p,
                    Name = Path.GetFileName(p),
                    Folder = Path.GetDirectoryName(p)?.Replace('\\', '/') is { Length: > 0 } dir ? dir : "/",
                    Change = change,
                    Progress = change == null ? "✓" : CountBlocks(p),
                });
            }

            var auto = new List<string>();
            foreach (var c in wc.Staged)
            {
                if (!paths.Contains(c.Path))
                    auto.Add(c.Path);
            }

            selectPath ??= _selectedFile?.Path;
            if (_advanceToNextFile || selectPath == null)
            {
                selectPath = files.Find(x => !x.IsResolved)?.Path ?? selectPath;
                _advanceToNextFile = false;
            }

            _isRefreshing = true;
            Files = files;
            AutoMergedFiles = auto;
            SelectedFile = files.Find(x => x.Path == selectPath) ?? (files.Count > 0 ? files[0] : null);
            _isRefreshing = false;
            NotifyProgressChanged();
        }

        public void KeepCurrent() => Editor?.ToggleCurrentRegionSide(true);
        public void KeepIncoming() => Editor?.ToggleCurrentRegionSide(false);
        public void KeepBoth() => Editor?.ResolveCurrentRegion(Models.ConflictResolution.UseBothMineFirst);
        public void GotoNextConflict() => Editor?.GotoRegion(1);
        public void GotoPrevConflict() => Editor?.GotoRegion(-1);

        public async Task MarkResolvedAsync()
        {
            if (!CanMarkResolved || _isBusy)
                return;

            IsBusy = true;
            _advanceToNextFile = true;
            if (!await _detail.SaveAndStageAsync())
                _advanceToNextFile = false;
            IsBusy = false;
        }

        public async Task UseWholeFileAsync(bool incoming)
        {
            if (_detail == null || _isBusy)
                return;

            IsBusy = true;
            _advanceToNextFile = true;
            if (incoming)
                await _detail.UseTheirsAsync();
            else
                await _detail.UseMineAsync();
            IsBusy = false;
        }

        public async Task OpenExternalMergeToolAsync()
        {
            if (_detail != null)
                await _detail.MergeExternalAsync();
        }

        public async Task FinishAsync()
        {
            if (!IsAllResolved || _isBusy)
                return;

            IsBusy = true;
            await _repo.ContinueMergeAsync();
            IsBusy = false;
        }

        public async Task AbortAsync()
        {
            IsAbortConfirmVisible = false;
            IsBusy = true;
            await _repo.AbortMergeAsync();
            IsBusy = false;
            _repo.CloseConflictResolver();
        }

        public void Close()
        {
            _repo.CloseConflictResolver();
        }

        private ConflictSide CreateSide(string name, int colorIdx)
        {
            var pens = Models.CommitGraph.Pens;
            var brush = pens.Count > 0 ? pens[colorIdx % pens.Count].Brush : Brushes.Gray;
            var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            return new ConflictSide()
            {
                Name = name,
                Brush = brush,
                Background = new SolidColorBrush(Color.FromArgb(0x22, color.R, color.G, color.B)),
            };
        }

        private string CountBlocks(string path)
        {
            if (_detail?.Editor is { } editor && editor.FilePath == path)
                return $"{editor.ConflictRegions.Count - editor.UnsolvedCount}/{editor.ConflictRegions.Count}";

            try
            {
                var full = Path.Combine(_repo.FullPath, path);
                if (!File.Exists(full) || new FileInfo(full).Length > 4 * 1024 * 1024)
                    return "!";

                var count = 0;
                foreach (var line in File.ReadLines(full))
                {
                    if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                        count++;
                }

                return count > 0 ? $"0/{count}" : "!";
            }
            catch
            {
                return "!";
            }
        }

        private void UpdateDetail()
        {
            var file = _selectedFile;
            if (file is not { Change: { } change })
            {
                Detail = null;
            }
            else if (_detail == null || !_detail.IsSameAs(change))
            {
                Detail = new Conflict(_repo, _repo.WorkingCopy, change);
                QueryLastCommits(file.Path);
            }

            OnPropertyChanged(nameof(WhyDescription));
            OnPropertyChanged(nameof(ResultDescription));
            OnPropertyChanged(nameof(StageCommand));
            NotifyFileStateChanged();
        }

        private void QueryLastCommits(string path)
        {
            Current.LastCommit = null;
            Incoming.LastCommit = null;

            var repo = _repo.FullPath;
            var incomingRev = _incomingRev;
            var isMerge = _op == "Merge";
            Task.Run(async () =>
            {
                var quoted = path.Quoted();
                Models.Commit current = null;
                Models.Commit incoming = null;

                if (!string.IsNullOrEmpty(incomingRev))
                {
                    var mergeBase = await new Commands.MergeBase(repo, "HEAD", incomingRev).GetResultAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(mergeBase))
                    {
                        current = await QueryFirstCommitAsync(repo, $"-1 {mergeBase}..HEAD -- {quoted}").ConfigureAwait(false);

                        // A merge brings a whole branch, the other operations replay a single commit.
                        incoming = isMerge
                            ? await QueryFirstCommitAsync(repo, $"-1 {mergeBase}..{incomingRev} -- {quoted}").ConfigureAwait(false)
                            : await QueryFirstCommitAsync(repo, $"-1 {incomingRev}").ConfigureAwait(false);
                    }
                }

                current ??= await QueryFirstCommitAsync(repo, $"-1 HEAD -- {quoted}").ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (_selectedFile?.Path != path)
                        return;

                    Current.LastCommit = current;
                    Incoming.LastCommit = incoming;
                });
            });
        }

        private static async Task<Models.Commit> QueryFirstCommitAsync(string repo, string limits)
        {
            var commits = await new Commands.QueryCommits(repo, limits, false).GetResultAsync().ConfigureAwait(false);
            return commits.Count > 0 ? commits[0] : null;
        }

        private void OnDetailPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Conflict.Editor))
            {
                OnPropertyChanged(nameof(Editor));
                OnEditorChanged();
            }
        }

        private void OnEditorChanged()
        {
            if (_editor != null)
                _editor.PropertyChanged -= OnEditorPropertyChanged;

            _editor = _detail?.Editor;
            if (_editor != null)
                _editor.PropertyChanged += OnEditorPropertyChanged;

            NotifyFileStateChanged();
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MergeConflictEditor.UnsolvedCount))
                NotifyFileStateChanged();
        }

        private void NotifyFileStateChanged()
        {
            if (_selectedFile is { IsResolved: false } && Editor is { ConflictRegions.Count: > 0 } editor)
                _selectedFile.Progress = $"{editor.ConflictRegions.Count - editor.UnsolvedCount}/{editor.ConflictRegions.Count}";

            OnPropertyChanged(nameof(FileTag));
            OnPropertyChanged(nameof(IsFileTagResolved));
            OnPropertyChanged(nameof(CanMarkResolved));
            OnPropertyChanged(nameof(MarkResolvedLabel));
        }

        private void NotifyProgressChanged()
        {
            OnPropertyChanged(nameof(ResolvedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(IsAllResolved));
            OnPropertyChanged(nameof(ProgressRatio));
            OnPropertyChanged(nameof(ProgressLabel));
            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(AbortDescription));
            NotifyFileStateChanged();
        }

        private readonly Repository _repo;
        private readonly string _op;
        private readonly string _opNoun;
        private readonly string _incomingRev;
        private List<ConflictFile> _files = [];
        private List<string> _autoMergedFiles = [];
        private ConflictFile _selectedFile = null;
        private Conflict _detail = null;
        private MergeConflictEditor _editor = null;
        private bool _advanceToNextFile = false;
        private bool _isRefreshing = false;
        private bool _isAbortConfirmVisible = false;
        private bool _isBusy = false;
    }
}
