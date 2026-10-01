using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Conflict : ObservableObject
    {
        public string Marker
        {
            get => _change.ConflictMarker;
        }

        public string Description
        {
            get => _change.ConflictDesc;
        }

        public Models.ConflictFileState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        public object Theirs
        {
            get => _theirs;
            private set => SetProperty(ref _theirs, value);
        }

        public object Mine
        {
            get => _mine;
            private set => SetProperty(ref _mine, value);
        }

        public string FilePath
        {
            get => _change.Path;
        }

        /// <summary>
        ///     Three-way resolver of a text conflict, shown in place of the summary card (null for other conflicts).
        /// </summary>
        public MergeConflictEditor Editor
        {
            get => _editor;
            private set => SetProperty(ref _editor, value);
        }

        public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
        {
            _repo = repo;
            _wc = wc;
            _change = change;
            _lastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(repo.FullPath, change.Path));

            Task.Run(async () =>
            {
                _head = new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResult();

                var (mine, theirs) = wc.InProgressContext switch
                {
                    CherryPickInProgress cherryPick => (_head, cherryPick.Head),
                    RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                    RevertInProgress revert => (_head, revert.Head),
                    MergeInProgress merge => (_head, merge.Source),
                    _ => (_head, (object)"Stash or Patch"),
                };

                var state = Models.ConflictFileState.Unknown;
                if ((_change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified) && !Directory.Exists(Path.Combine(_repo.FullPath, _change.Path)))
                    state = await new Commands.QueryConflictFileState(repo.FullPath, change)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                var editor = state == Models.ConflictFileState.UnmergedText ? new MergeConflictEditor(repo, _head, change.Path) : null;

                Dispatcher.UIThread.Post(() =>
                {
                    State = state;
                    Mine = mine;
                    Theirs = theirs;
                    Editor = editor;
                });
            });
        }

        /// <summary>
        ///     Still the same conflict (same file, same reason, not modified on disk since): refreshing the working copy
        ///     then keeps this view model, so the resolver keeps its scroll position and the chunks already picked.
        /// </summary>
        public bool IsSameAs(Models.Change change)
        {
            return change.Path.Equals(_change.Path, StringComparison.Ordinal) &&
                change.ConflictReason == _change.ConflictReason &&
                File.GetLastWriteTimeUtc(Path.Combine(_repo.FullPath, change.Path)) == _lastWriteTime;
        }

        public async Task UseTheirsAsync()
        {
            await _wc.UseTheirsAsync([_change]);
        }

        public async Task UseMineAsync()
        {
            await _wc.UseMineAsync([_change]);
        }

        public MergeConflictEditor CreateOpenMergeEditorRequest()
        {
            return _state == Models.ConflictFileState.UnmergedText ? new MergeConflictEditor(_repo, _head, _change.Path) : null;
        }

        public async Task<bool> SaveAndStageAsync()
        {
            return _editor != null && await _editor.SaveAndStageAsync();
        }

        public async Task MergeExternalAsync()
        {
            if (_state == Models.ConflictFileState.UnmergedText)
                await _wc.UseExternalMergeToolAsync(_change);
        }

        private Repository _repo = null;
        private WorkingCopy _wc = null;
        private Models.Change _change = null;
        private Models.Commit _head = null;
        private Models.ConflictFileState _state = Models.ConflictFileState.Unknown;
        private object _mine = null;
        private object _theirs = null;
        private MergeConflictEditor _editor = null;
        private DateTime _lastWriteTime;
    }
}
