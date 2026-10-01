using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum BranchDropAction
    {
        Merge,
        Rebase,
        InteractiveRebase,
        PullRequest,
    }

    public class BranchDropOption : ObservableObject
    {
        public BranchDropAction Action { get; init; }
        public string Title { get; init; } = string.Empty;
        public string GitTerm { get; init; } = string.Empty;
        public bool IsEnabled { get; init; } = true;
        public bool HasSeparator { get; init; } = false;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _description = string.Empty;
    }

    /// <summary>
    ///     What to do after dropping a branch onto another one (merge, rebase...).
    /// </summary>
    public class BranchDrop : ObservableObject
    {
        public Repository Repository => _repo;
        public Models.Branch Source { get; }
        public Models.Branch Target { get; }
        public string SourceName => Source.FriendlyName;
        public string TargetName => Target.FriendlyName;
        public IBrush SourceBrush => Models.BranchColor.BrushOf(Source.Name);
        public IBrush TargetBrush => Models.BranchColor.BrushOf(Target.Name);
        public List<BranchDropOption> Options { get; } = [];

        public BranchDropOption SelectedOption
        {
            get => _selectedOption;
            set => SetProperty(ref _selectedOption, value);
        }

        public string ConflictDescription
        {
            get => _conflictCount switch
            {
                TESTING => App.Text("BranchDrop.Testing"),
                0 => App.Text("BranchDrop.NoConflicts"),
                1 => App.Text("BranchDrop.Conflict"),
                > 1 => App.Text("BranchDrop.Conflicts", _conflictCount),
                _ => App.Text("BranchDrop.Unknown"),
            };
        }

        public IBrush ConflictBrush
        {
            get
            {
                var key = _conflictCount switch
                {
                    0 => "Brush.Added",
                    > 0 => "Brush.Warning",
                    _ => "Brush.FG3",
                };

                return App.Current?.FindResource(key) as IBrush ?? Brushes.Gray;
            }
        }

        public BranchDrop(Repository repo, Models.Branch source, Models.Branch target)
        {
            _repo = repo;
            Source = source;
            Target = target;

            var current = repo.CurrentBranch;
            var targetIsCurrent = target.IsLocal && current != null && current.Name.Equals(target.Name, StringComparison.Ordinal);
            var sourceIsCurrent = source.IsLocal && current != null && current.Name.Equals(source.Name, StringComparison.Ordinal);

            var mergeDesc = App.Text("BranchDrop.Merge.Desc");
            if (!target.IsLocal)
                mergeDesc = App.Text("BranchDrop.Merge.Remote");
            else if (!targetIsCurrent)
                mergeDesc = $"{mergeDesc} {App.Text("BranchDrop.SwitchFirst", target.Name)}";

            Options.Add(new BranchDropOption()
            {
                Action = BranchDropAction.Merge,
                Title = App.Text("BranchDrop.Merge", target.FriendlyName),
                GitTerm = "merge ↵",
                IsEnabled = target.IsLocal,
                Description = mergeDesc,
            });

            _rebase = new BranchDropOption()
            {
                Action = BranchDropAction.Rebase,
                Title = App.Text("BranchDrop.Rebase", target.FriendlyName),
                GitTerm = "rebase",
                IsEnabled = source.IsLocal,
                Description = source.IsLocal ? RebaseDescription(-1, sourceIsCurrent) : App.Text("BranchDrop.Rebase.Remote"),
            };
            Options.Add(_rebase);

            Options.Add(new BranchDropOption()
            {
                Action = BranchDropAction.InteractiveRebase,
                Title = App.Text("BranchDrop.Interactive"),
                GitTerm = "rebase -i",
                IsEnabled = source.IsLocal,
                Description = source.IsLocal
                    ? (sourceIsCurrent ? App.Text("BranchDrop.Interactive.Desc") : $"{App.Text("BranchDrop.Interactive.Desc")} {App.Text("BranchDrop.SwitchFirst", source.Name)}")
                    : App.Text("BranchDrop.Rebase.Remote"),
            });

            if (repo.TryGetCreatePullRequestURL(source, target, out var url))
            {
                _pullRequestURL = url;
                Options.Add(new BranchDropOption()
                {
                    Action = BranchDropAction.PullRequest,
                    Title = App.Text("BranchDrop.PullRequest", target.Name),
                    HasSeparator = true,
                });
            }

            _selectedOption = Options.Find(x => x.IsEnabled);
            _sourceIsCurrent = sourceIsCurrent;
            _conflictCount = TESTING;

            Task.Run(async () =>
            {
                var conflicts = await new Commands.MergeTreeConflicts(repo.FullPath, source.FriendlyName, target.FriendlyName)
                    .GetConflictCountAsync()
                    .ConfigureAwait(false);

                var commits = await new Commands.CountCommits(repo.FullPath, $"{target.FriendlyName}..{source.FriendlyName}")
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _conflictCount = conflicts;
                    OnPropertyChanged(nameof(ConflictDescription));
                    OnPropertyChanged(nameof(ConflictBrush));

                    if (Source.IsLocal)
                        _rebase.Description = RebaseDescription(commits, _sourceIsCurrent);
                });
            });
        }

        public async Task ExecuteAsync(BranchDropOption option, Func<Models.Commit, Task> startInteractiveRebase)
        {
            if (option is not { IsEnabled: true })
                return;

            switch (option.Action)
            {
                case BranchDropAction.Merge:
                    if (await _repo.SwitchToBranchIfNeededAsync(Target) && _repo.CanCreatePopup())
                        _repo.ShowPopup(new Merge(_repo, Source, Target.Name, false));
                    break;
                case BranchDropAction.Rebase:
                    if (await _repo.SwitchToBranchIfNeededAsync(Source) && _repo.CanCreatePopup())
                        _repo.ShowPopup(new Rebase(_repo, Source, Target));
                    break;
                case BranchDropAction.InteractiveRebase:
                    if (await _repo.SwitchToBranchIfNeededAsync(Source))
                    {
                        var on = await new Commands.QuerySingleCommit(_repo.FullPath, Target.Head).GetResultAsync();
                        if (on != null)
                            await startInteractiveRebase(on);
                    }
                    break;
                case BranchDropAction.PullRequest:
                    Native.OS.OpenBrowser(_pullRequestURL);
                    break;
            }
        }

        private string RebaseDescription(int commits, bool sourceIsCurrent)
        {
            var desc = commits switch
            {
                1 => App.Text("BranchDrop.Rebase.DescOne", Target.FriendlyName),
                > 1 => App.Text("BranchDrop.Rebase.DescCount", commits, Target.FriendlyName),
                _ => App.Text("BranchDrop.Rebase.Desc", Target.FriendlyName),
            };

            return sourceIsCurrent ? desc : $"{desc} {App.Text("BranchDrop.SwitchFirst", Source.Name)}";
        }

        private const int TESTING = -2;

        private readonly Repository _repo;
        private readonly BranchDropOption _rebase;
        private readonly string _pullRequestURL;
        private readonly bool _sourceIsCurrent;
        private BranchDropOption _selectedOption;
        private int _conflictCount;
    }
}
