using Avalonia.Media;

namespace SourceGit.Models
{
    /// <summary>
    ///     Stable color per branch, shared by the sidebar, ref pills, graph lanes and inspector.
    ///     Callers pass the branch name without its remote prefix, so `origin/foo` and `foo` match.
    /// </summary>
    public static class BranchColor
    {
        public static int IndexOf(string name)
        {
            var count = CommitGraph.Pens.Count;
            if (count <= 1 || string.IsNullOrEmpty(name))
                return 0;

            if (name is "main" or "master" or "develop" or "trunk")
                return 0;

            // FNV-1a, so the color doesn't change between runs.
            var hash = 2166136261u;
            foreach (var c in name)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return 1 + (int)(hash % (uint)(count - 1));
        }

        public static IBrush BrushOf(string name)
        {
            var pens = CommitGraph.Pens;
            return pens.Count == 0 ? Brushes.Gray : pens[IndexOf(name)].Brush;
        }
    }
}
