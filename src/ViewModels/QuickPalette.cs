using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Media;

namespace SourceGit.ViewModels
{
    public enum QuickPaletteItemKind
    {
        Action,
        Branch,
        Commit,
    }

    public class QuickPaletteItem
    {
        public QuickPaletteItemKind Kind { get; init; }
        public string Group { get; init; } = string.Empty;
        public bool IsGroupStart { get; set; } = false;
        public string Icon { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string GitCommand { get; init; } = string.Empty;
        public string Hint { get; init; } = string.Empty;
        public IBrush Brush { get; init; } = null;
        public string SHA { get; init; } = string.Empty;

        public bool IsAction => Kind == QuickPaletteItemKind.Action;
        public bool IsBranch => Kind == QuickPaletteItemKind.Branch;
        public bool IsCommit => Kind == QuickPaletteItemKind.Commit;
        public bool HasGitCommand => !string.IsNullOrEmpty(GitCommand);

        internal string Keywords { get; init; } = string.Empty;
        internal bool KeepOpen { get; init; } = false;
        internal Action Run { get; init; }
        internal int Score { get; set; }
        internal ulong Time { get; init; }

        internal QuickPaletteItem Clone() => (QuickPaletteItem)MemberwiseClone();
    }

    /// <summary>
    ///     ⌘P: fuzzy search over actions, branches and commits of the current repository.
    /// </summary>
    public class QuickPalette : ICommandPalette
    {
        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    Update();
            }
        }

        public List<QuickPaletteItem> Items
        {
            get => _items;
            private set => SetProperty(ref _items, value);
        }

        public QuickPaletteItem Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        public QuickPalette(Launcher launcher, Repository repo, string filter = "")
        {
            _launcher = launcher;
            _repo = repo;
            _actions = BuildActions();
            _filter = filter;
            Update();
        }

        public void SelectNext(int step)
        {
            if (_items.Count == 0)
                return;

            var idx = _selected != null ? _items.IndexOf(_selected) : -1;
            idx = Math.Clamp(idx + step, 0, _items.Count - 1);
            Selected = _items[idx];
        }

        public void Exec(QuickPaletteItem item = null)
        {
            item ??= _selected;
            if (item?.Run == null)
                return;

            if (!item.KeepOpen)
                Close();

            item.Run();
        }

        /// <summary>
        ///     ⌘↵: returns the Git command behind the selected entry (and says it was copied).
        /// </summary>
        public string TakeGitCommand()
        {
            if (_selected is not { HasGitCommand: true } item)
                return null;

            var cmd = $"git {item.GitCommand}";
            Message = App.Text("Palette.Copied", cmd);
            return cmd;
        }

        private List<QuickPaletteItem> BuildActions()
        {
            var repo = _repo;
            var current = repo.CurrentBranch?.Name ?? "HEAD";
            var remote = repo.FetchRemote ?? "origin";
            var actions = new List<QuickPaletteItem>();

            void Add(string key, string icon, string git, string keywords, Action run, bool keepOpen = false, params object[] args)
            {
                actions.Add(new QuickPaletteItem()
                {
                    Kind = QuickPaletteItemKind.Action,
                    Group = App.Text("Palette.Group.Actions"),
                    Icon = icon,
                    Title = App.Text($"Palette.{key}", args),
                    Subtitle = App.Text($"Palette.{key}.Desc", args),
                    GitCommand = git,
                    Keywords = keywords,
                    KeepOpen = keepOpen,
                    Run = run,
                });
            }

            Add("Fetch", "Icons.Fetch", "fetch", "fetch download update", async () => await repo.FetchAsync(false));

            if (!repo.IsBare)
            {
                Add("Pull", "Icons.Pull", "pull", "pull update download", async () => await repo.PullAsync(false), false, current);
                Add("Push", "Icons.Push", "push", "push upload send publish", async () => await repo.PushAsync(false), false, remote);
                Add("Checkout", "Icons.Check", "switch", "checkout switch branch go", () => new CheckoutCommandPalette(repo).Open(), true);
                Add("Merge", "Icons.Merge", "merge", "merge join combine", () => new MergeCommandPalette(repo).Open(), true, current);
                Add("Squash", "Icons.SquashIntoParent", "merge --squash", "squash merge combine", () => Filter = "squash ", true);
                Add("Rebase", "Icons.Rebase", "rebase", "rebase replay linear", () => Filter = "rebase ", true, current);
            }

            Add("CreateBranch", "Icons.Branch.Add", "branch", "create new branch", () => repo.CreateNewBranch());
            Add("CreateTag", "Icons.Tag.Add", "tag", "create new tag release", () => repo.CreateNewTag());

            if (!repo.IsBare)
            {
                Add("Stash", "Icons.Stashes.Add", "stash", "stash save shelve aside", async () => await repo.StashAllAsync(false));
                if (repo.StashesCount > 0)
                    Add("Pop", "Icons.Stashes", "stash pop", "stash pop apply restore", () => repo.PopLatestStash());

                if (repo.GetSelectedCommitInHistory() is { } commit)
                {
                    var sha = commit.SHA.Substring(0, Math.Min(7, commit.SHA.Length));
                    Add("CherryPick", "Icons.CherryPick", "cherry-pick", "cherry pick copy commit", async () => await repo.Histories.CherryPickAsync(commit), false, sha);
                    Add("Revert", "Icons.Undo", "revert", "revert undo commit", () =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new Revert(repo, commit));
                    }, false, sha);
                }

                Add("ApplyPatch", "Icons.ApplyPatch", "apply", "apply patch diff", () => repo.ApplyPatch());
                Add("Discard", "Icons.Undo", "restore .", "discard reset clean changes", () => repo.WorkingCopy.DiscardAllChanges());
            }

            Add("Blame", "Icons.Blame", "blame", "blame annotate who", () => new BlameCommandPalette(repo.FullPath).Open(), true);
            Add("FileHistory", "Icons.Histories", "log --follow", "file history log", () => new FileHistoryCommandPalette(repo.FullPath).Open(), true);
            Add("Compare", "Icons.Compare", "diff", "compare diff", () => new CompareCommandPalette(repo, null).Open(), true);
            Add("OpenFile", "Icons.OpenWith", string.Empty, "open file", () => new OpenFileCommandPalette(repo.FullPath).Open(), true);
            Add("CustomActions", "Icons.Action", string.Empty, "custom actions scripts", () => new ExecuteCustomActionCommandPalette(repo).Open(), true);
            Add("Terminal", "Icons.Terminal", string.Empty, "terminal shell console", () => Native.OS.OpenTerminal(repo.FullPath));
            Add(OperatingSystem.IsMacOS() ? "Reveal" : "RevealOther", "Icons.Explore", string.Empty, "finder explorer folder reveal", () => Native.OS.OpenInFileManager(repo.FullPath));
            Add("GotoRepository", "Icons.Repositories", string.Empty, "repository tab switch open", () => new LauncherPagesCommandPalette(_launcher).Open(), true);

            return actions;
        }

        private void Update()
        {
            var filter = _filter ?? string.Empty;
            var commandsOnly = filter.StartsWith('>');
            if (commandsOnly)
                filter = filter.Substring(1);

            filter = filter.TrimStart();

            // A leading verb ("merge feat") applies it to the matching branches.
            var verb = string.Empty;
            var branchQuery = filter;
            var space = filter.IndexOf(' ');
            var firstWord = (space > 0 ? filter.Substring(0, space) : filter).ToLowerInvariant();
            if (firstWord is "merge" or "squash" or "rebase" or "checkout" or "switch")
            {
                verb = firstWord;
                branchQuery = space > 0 ? filter.Substring(space + 1) : string.Empty;
            }

            var items = new List<QuickPaletteItem>();

            // Actions
            var actions = new List<QuickPaletteItem>();
            foreach (var action in _actions)
            {
                var score = string.IsNullOrWhiteSpace(filter)
                    ? 1
                    : Math.Max(Models.FuzzyMatch.Score(filter, action.Title), Models.FuzzyMatch.Score(filter, action.Keywords) - 50);

                if (score >= 0)
                {
                    action.Score = score;
                    actions.Add(action);
                }
            }

            // Close scores keep the declared order (the most common action of a family comes first).
            if (!string.IsNullOrWhiteSpace(filter))
                actions = actions.OrderByDescending(x => x.Score / 50).ToList();
            AddGroup(items, actions, string.IsNullOrWhiteSpace(filter) ? 8 : 6);

            if (!commandsOnly && !_repo.IsBare)
            {
                AddGroup(items, FindBranches(verb, branchQuery, !string.IsNullOrEmpty(verb) || !string.IsNullOrWhiteSpace(filter)), 6);

                if (string.IsNullOrEmpty(verb) && filter.Trim().Length >= 2)
                    AddGroup(items, FindCommits(filter.Trim()), 5);
            }

            Items = items;
            Selected = items.Count > 0 ? items[0] : null;
            Message = string.Empty;
        }

        private List<QuickPaletteItem> FindBranches(string verb, string query, bool enabled)
        {
            var result = new List<QuickPaletteItem>();
            if (!enabled)
                return result;

            var current = _repo.CurrentBranch;
            var hint = verb switch
            {
                "merge" => App.Text("Palette.Branch.Merge"),
                "squash" => App.Text("Palette.Branch.Squash"),
                "rebase" => App.Text("Palette.Branch.Rebase"),
                _ => App.Text("Palette.Branch.Checkout"),
            };

            foreach (var branch in _repo.Branches)
            {
                if (branch.IsDetachedHead || (current != null && branch.FullName.Equals(current.FullName, StringComparison.Ordinal)))
                    continue;

                var score = Models.FuzzyMatch.Score(query, branch.FriendlyName);
                if (score < 0)
                    continue;

                var target = branch;
                result.Add(new QuickPaletteItem()
                {
                    Kind = QuickPaletteItemKind.Branch,
                    Group = App.Text("Palette.Group.Branches"),
                    Title = branch.FriendlyName,
                    Hint = hint,
                    Brush = Models.BranchColor.BrushOf(branch.Name),
                    GitCommand = verb switch
                    {
                        "merge" => $"merge {branch.FriendlyName}",
                        "squash" => $"merge --squash {branch.FriendlyName}",
                        "rebase" => $"rebase {branch.FriendlyName}",
                        _ => $"switch {branch.Name}",
                    },
                    Score = score + (branch.IsLocal ? 20 : 0),
                    Time = branch.CommitterDate,
                    Run = () => RunOnBranch(verb, target),
                });
            }

            if (string.IsNullOrWhiteSpace(query))
                result.Sort((l, r) => r.Time.CompareTo(l.Time));
            else
                result.Sort((l, r) => r.Score.CompareTo(l.Score));

            return result;
        }

        private List<QuickPaletteItem> FindCommits(string query)
        {
            var result = new List<QuickPaletteItem>();
            var commits = _repo.Histories?.Commits;
            if (commits == null)
                return result;

            foreach (var commit in commits)
            {
                var score = commit.SHA.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    ? 2000
                    : Models.FuzzyMatch.Score(query, commit.Subject);

                if (score < 0)
                    continue;

                var sha = commit.SHA;
                result.Add(new QuickPaletteItem()
                {
                    Kind = QuickPaletteItemKind.Commit,
                    Group = App.Text("Palette.Group.Commits"),
                    Title = commit.Subject,
                    SHA = sha.Substring(0, Math.Min(7, sha.Length)),
                    GitCommand = $"show {sha.Substring(0, Math.Min(7, sha.Length))}",
                    Score = score,
                    Run = () => _repo.NavigateToCommit(sha),
                });

                if (result.Count >= 200)
                    break;
            }

            result.Sort((l, r) => r.Score.CompareTo(l.Score));
            return result;
        }

        private void RunOnBranch(string verb, Models.Branch branch)
        {
            var current = _repo.CurrentBranch;
            switch (verb)
            {
                case "merge" or "squash":
                    if (current != null && _repo.CanCreatePopup())
                    {
                        var merge = new Merge(_repo, branch, current.Name, false);
                        if (verb == "squash")
                            merge.Mode = Models.MergeMode.Squash;
                        _repo.ShowPopup(merge);
                    }
                    break;
                case "rebase":
                    if (current != null && _repo.CanCreatePopup())
                        _repo.ShowPopup(new Rebase(_repo, current, branch));
                    break;
                default:
                    _ = _repo.CheckoutBranchAsync(branch);
                    break;
            }
        }

        private static void AddGroup(List<QuickPaletteItem> items, List<QuickPaletteItem> group, int max)
        {
            for (var i = 0; i < group.Count && i < max; i++)
            {
                var item = group[i].Clone();
                item.IsGroupStart = i == 0;
                items.Add(item);
            }
        }

        private readonly Launcher _launcher;
        private readonly Repository _repo;
        private readonly List<QuickPaletteItem> _actions;
        private string _filter = string.Empty;
        private List<QuickPaletteItem> _items = [];
        private QuickPaletteItem _selected = null;
        private string _message = string.Empty;
    }
}
