using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     A GitHub pull request shown in the history inspector: details from the GitHub API, changed files computed
    ///     locally (after fetching `refs/pull/N/head`) and the diff of a file in the center of the history page.
    /// </summary>
    public class PullRequestDetail : ObservableObject
    {
        public Models.PullRequest PullRequest
        {
            get;
        }

        public Models.PullRequestInfo Info
        {
            get => _info;
            private set => SetProperty(ref _info, value);
        }

        public bool IsLoadingInfo
        {
            get => _isLoadingInfo;
            private set => SetProperty(ref _isLoadingInfo, value);
        }

        public bool IsLoadingChanges
        {
            get => _isLoadingChanges;
            private set => SetProperty(ref _isLoadingChanges, value);
        }

        /// <summary>
        ///     Shown instead of the files when the details or the commits of the PR could not be loaded.
        /// </summary>
        public string ChangesError
        {
            get => _changesError;
            private set => SetProperty(ref _changesError, value);
        }

        public List<Models.Change> Changes
        {
            get => _changes;
            private set => SetProperty(ref _changes, value);
        }

        public List<CommitInspectorRow> InspectorRows
        {
            get => _inspectorRows;
            private set => SetProperty(ref _inspectorRows, value);
        }

        public string TotalAdded
        {
            get => _totalAdded;
            private set => SetProperty(ref _totalAdded, value);
        }

        public string TotalDeleted
        {
            get => _totalDeleted;
            private set => SetProperty(ref _totalDeleted, value);
        }

        public bool IsInspectorTreeMode
        {
            get => _sharedData.IsInspectorTreeMode;
            set
            {
                if (value != _sharedData.IsInspectorTreeMode)
                {
                    _sharedData.IsInspectorTreeMode = value;
                    OnPropertyChanged();
                    BuildInspectorRows();
                }
            }
        }

        public string Branches
        {
            get
            {
                if (_info == null)
                    return string.Empty;

                var head = _info.IsFromFork ? $"{_info.HeadRepo.Split('/')[0]}:{_info.HeadRef}" : _info.HeadRef;
                return $"{head} → {_info.BaseRef}";
            }
        }

        public string Dates
        {
            get
            {
                if (_info == null)
                    return string.Empty;

                var opened = App.Text("PullRequest.Opened", Models.RelativeTime.Format(_info.CreatedAt));
                if (_info.MergedAt is { } merged)
                    return $"{opened} · {App.Text("PullRequest.MergedAt", Models.RelativeTime.Format(merged))}";
                if (_info.ClosedAt is { } closed)
                    return $"{opened} · {App.Text("PullRequest.ClosedAt", Models.RelativeTime.Format(closed))}";
                return opened;
            }
        }

        public string Stats
        {
            get
            {
                if (_info == null)
                    return string.Empty;

                var commits = _info.Commits == 1 ? App.Text("PullRequest.Commit") : App.Text("PullRequest.Commits", _info.Commits);
                var files = _info.ChangedFiles == 1 ? App.Text("PullRequest.File") : App.Text("PullRequest.Files", _info.ChangedFiles);
                return $"{commits} · {files}";
            }
        }

        public string Additions
        {
            get => _info == null ? string.Empty : $"+{_info.Additions}";
        }

        public string Deletions
        {
            get => _info == null ? string.Empty : $"−{_info.Deletions}";
        }

        public bool HasBody
        {
            get => !string.IsNullOrWhiteSpace(_info?.Body);
        }

        public string Body
        {
            get => _info?.Body?.Replace("\r\n", "\n").Trim() ?? string.Empty;
        }

        /// <summary>
        ///     The PR itself (header + rendered description) is shown in the center of the history page, under any file diff.
        /// </summary>
        public bool IsOverviewVisible
        {
            get => _isOverviewVisible;
            private set
            {
                if (SetProperty(ref _isOverviewVisible, value))
                    OnPropertyChanged(nameof(IsOverviewShown));
            }
        }

        /// <summary>
        ///     The description is what the center shows right now (no file diff over it).
        /// </summary>
        public bool IsOverviewShown
        {
            get => _isOverviewVisible && _centerDiff == null;
        }

        /// <summary>
        ///     Diff of one file of the PR, displayed in the center of the history page instead of the graph.
        /// </summary>
        public DiffContext CenterDiff
        {
            get => _centerDiff;
            private set
            {
                if (SetProperty(ref _centerDiff, value))
                    OnPropertyChanged(nameof(IsOverviewShown));
            }
        }

        public PullRequestDetail(Repository repo, CommitDetailSharedData sharedData, Models.PullRequest pr)
        {
            _repo = repo;
            _sharedData = sharedData;
            PullRequest = pr;
            _ = LoadAsync();
        }

        public void OpenDiffInCenter(Models.Change change)
        {
            if (string.IsNullOrEmpty(_baseRevision) || string.IsNullOrEmpty(_headRevision))
                return;

            CenterDiff = new DiffContext(_repo.FullPath, new Models.DiffOption(_baseRevision, _headRevision, change), _centerDiff);
        }

        public void CloseCenterDiff()
        {
            CenterDiff = null;
        }

        public void ShowOverview()
        {
            CenterDiff = null;
            IsOverviewVisible = true;
        }

        public void HideOverview()
        {
            CenterDiff = null;
            IsOverviewVisible = false;
        }

        public void OpenInBrowser()
        {
            if (!string.IsNullOrEmpty(PullRequest.Url))
                Native.OS.OpenBrowser(PullRequest.Url);
        }

        public async Task CheckoutAsync()
        {
            if (_info == null)
                return;

            // Same repository: check out (or create a tracking branch for) its remote branch.
            if (!_info.IsFromFork)
            {
                var remoteBranch = _repo.Branches.Find(x =>
                    !x.IsLocal &&
                    x.Remote.Equals(PullRequest.Remote, StringComparison.Ordinal) &&
                    x.Name.Equals(_info.HeadRef, StringComparison.Ordinal));
                if (remoteBranch != null)
                {
                    await _repo.CheckoutBranchAsync(remoteBranch);
                    return;
                }
            }

            // Fork, or branch not fetched: create a local branch on the PR head commit.
            if (string.IsNullOrEmpty(_headRevision) || !_repo.CanCreatePopup())
                return;

            var head = await new Commands.QuerySingleCommit(_repo.FullPath, _headRevision).GetResultAsync();
            if (head != null)
                _repo.ShowPopup(new CreateBranch(_repo, head));
        }

        private async Task LoadAsync()
        {
            IsLoadingInfo = true;
            IsLoadingChanges = true;

            var info = await Models.PullRequestProvider.FetchInfoAsync(PullRequest).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                Info = info;
                IsLoadingInfo = false;
                OnPropertyChanged(nameof(Branches));
                OnPropertyChanged(nameof(Dates));
                OnPropertyChanged(nameof(Stats));
                OnPropertyChanged(nameof(Additions));
                OnPropertyChanged(nameof(Deletions));
                OnPropertyChanged(nameof(HasBody));
                OnPropertyChanged(nameof(Body));
            });

            if (info == null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ChangesError = App.Text("PullRequest.LoadFailed");
                    IsLoadingChanges = false;
                });
                return;
            }

            var (baseRevision, changes, stats) = await LoadChangesAsync(info).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                IsLoadingChanges = false;
                if (baseRevision == null)
                {
                    ChangesError = App.Text("PullRequest.FetchFailed");
                    return;
                }

                _baseRevision = baseRevision;
                _headRevision = info.HeadSha;
                _numStats = stats;
                Changes = changes;
                BuildInspectorRows();
            });
        }

        private async Task<(string, List<Models.Change>, Dictionary<string, Models.NumStat>)> LoadChangesAsync(Models.PullRequestInfo info)
        {
            var repo = _repo.FullPath;
            var remote = _repo.Remotes.Find(x => x.Name.Equals(PullRequest.Remote, StringComparison.Ordinal));
            if (remote == null || string.IsNullOrEmpty(info.HeadSha))
                return (null, null, null);

            // Fetch the PR commits (and its base commit when it is not known locally yet).
            if (!await HasCommitAsync(repo, info.HeadSha) || !await HasCommitAsync(repo, info.BaseSha))
            {
                await new Commands.FetchPullRequest(repo, remote, PullRequest.Number, info.BaseSha).ExecAsync().ConfigureAwait(false);
                if (!await HasCommitAsync(repo, info.HeadSha))
                    return (null, null, null);
            }

            // Like GitHub's "Files changed": from the merge base of the base branch to the PR head.
            var baseRef = await HasCommitAsync(repo, info.BaseSha) ? info.BaseSha : $"{remote.Name}/{info.BaseRef}";
            var mergeBase = await new Commands.MergeBase(repo, baseRef, info.HeadSha).GetResultAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(mergeBase))
                return (null, null, null);

            var changes = await new Commands.CompareRevisions(repo, mergeBase, info.HeadSha).ReadAsync().ConfigureAwait(false);
            var stats = await new Commands.QueryCommitNumstat(repo, mergeBase, info.HeadSha).GetResultAsync().ConfigureAwait(false);
            return (mergeBase, changes, stats);
        }

        private static async Task<bool> HasCommitAsync(string repo, string sha)
        {
            if (string.IsNullOrEmpty(sha))
                return false;

            return await new Commands.IsCommitSHA(repo, sha).GetResultAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Collapses or expands a folder of the files tree.
        /// </summary>
        public void ToggleFolder(string path)
        {
            if (!_collapsedFolders.Remove(path))
                _collapsedFolders.Add(path);

            BuildInspectorRows();
        }

        private void BuildInspectorRows()
        {
            (TotalAdded, TotalDeleted) = CommitInspectorRow.FormatTotals(_numStats);
            InspectorRows = CommitInspectorRow.Build(_changes, _numStats, _sharedData.IsInspectorTreeMode, _collapsedFolders);
        }

        private readonly Repository _repo;
        private readonly CommitDetailSharedData _sharedData;
        private Models.PullRequestInfo _info = null;
        private bool _isLoadingInfo = false;
        private bool _isLoadingChanges = false;
        private string _changesError = null;
        private List<Models.Change> _changes = [];
        private Dictionary<string, Models.NumStat> _numStats = [];
        private readonly HashSet<string> _collapsedFolders = [];
        private List<CommitInspectorRow> _inspectorRows = [];
        private string _totalAdded = string.Empty;
        private string _totalDeleted = string.Empty;
        private string _baseRevision = null;
        private string _headRevision = null;
        private DiffContext _centerDiff = null;
        private bool _isOverviewVisible = true;
    }
}
