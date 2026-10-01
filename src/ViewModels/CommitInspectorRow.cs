using System;
using System.Collections.Generic;

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

        /// <summary>
        ///     Folder path ("src/Views") of a folder row, used to collapse/expand it.
        /// </summary>
        public string FolderPath { get; init; } = string.Empty;

        public bool IsExpanded { get; init; } = true;

        /// <summary>
        ///     Rows for the first 100 changes. In tree mode: nested folders (folders first, then files), where a chain
        ///     of folders with a single sub-folder and no file is shown on one row ("app/code/Vendor"), like VS Code.
        /// </summary>
        public static List<CommitInspectorRow> Build(List<Models.Change> all, Dictionary<string, Models.NumStat> stats, bool treeMode, HashSet<string> collapsed = null)
        {
            var changes = all.Count > 100 ? all.GetRange(0, 100) : all;
            var rows = new List<CommitInspectorRow>();

            if (!treeMode)
            {
                foreach (var c in changes)
                    rows.Add(Create(c, c.Path, 0, stats));
                return rows;
            }

            var root = new Folder(string.Empty, string.Empty);
            foreach (var c in changes)
            {
                var folder = root;
                var parts = c.Path.Split('/');
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    if (!folder.Folders.TryGetValue(parts[i], out var sub))
                    {
                        sub = new Folder(parts[i], folder.Path.Length == 0 ? parts[i] : $"{folder.Path}/{parts[i]}");
                        folder.Folders.Add(parts[i], sub);
                    }

                    folder = sub;
                }

                folder.Files.Add((parts[^1], c));
            }

            Emit(root, 0, rows, stats, collapsed);
            return rows;
        }

        /// <summary>
        ///     "+N" / "−N" totals of a numstat, or empty strings while it is not loaded.
        /// </summary>
        public static (string Added, string Deleted) FormatTotals(Dictionary<string, Models.NumStat> stats)
        {
            if (stats.Count == 0)
                return (string.Empty, string.Empty);

            var added = 0;
            var deleted = 0;
            foreach (var stat in stats.Values)
            {
                added += stat.Added;
                deleted += stat.Deleted;
            }

            return ($"+{added}", $"−{deleted}");
        }

        private static void Emit(Folder folder, int depth, List<CommitInspectorRow> rows, Dictionary<string, Models.NumStat> stats, HashSet<string> collapsed)
        {
            foreach (var sub in folder.Folders.Values)
            {
                // Compact "a/b/c" when a folder only holds one sub-folder.
                var node = sub;
                var label = node.Name;
                while (node.Files.Count == 0 && node.Folders.Count == 1)
                {
                    foreach (var only in node.Folders.Values)
                        node = only;
                    label = $"{label}/{node.Name}";
                }

                var expanded = collapsed == null || !collapsed.Contains(node.Path);
                rows.Add(new CommitInspectorRow()
                {
                    IsFolder = true,
                    Label = label,
                    FolderPath = node.Path,
                    IsExpanded = expanded,
                    Indent = new Thickness(depth * INDENT, 0, 0, 0),
                });

                if (expanded)
                    Emit(node, depth + 1, rows, stats, collapsed);
            }

            folder.Files.Sort((l, r) => string.Compare(l.Name, r.Name, StringComparison.Ordinal));
            foreach (var (name, change) in folder.Files)
                rows.Add(Create(change, name, depth * INDENT, stats));
        }

        private static CommitInspectorRow Create(Models.Change change, string label, double indent, Dictionary<string, Models.NumStat> stats)
        {
            stats.TryGetValue(change.Path, out var stat);
            return new CommitInspectorRow()
            {
                Label = label,
                Change = change,
                Indent = new Thickness(indent, 0, 0, 0),
                Added = stat is { Added: > 0 } ? $"+{stat.Added}" : string.Empty,
                Deleted = stat is { Deleted: > 0 } ? $"−{stat.Deleted}" : string.Empty,
            };
        }

        private class Folder(string name, string path)
        {
            public string Name { get; } = name;
            public string Path { get; } = path;
            public SortedDictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);
            public List<(string Name, Models.Change Change)> Files { get; } = [];
        }

        private const double INDENT = 14;
    }
}
