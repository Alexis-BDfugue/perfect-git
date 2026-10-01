using System;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class CommitConverters
    {
        public static readonly FuncValueConverter<Models.Commit, IBrush> ToLaneBrush =
            new FuncValueConverter<Models.Commit, IBrush>(v =>
            {
                var pens = Models.CommitGraph.Pens;
                if (v == null || pens.Count == 0)
                    return Brushes.Gray;

                return pens[Math.Clamp(v.Color, 0, pens.Count - 1)].Brush;
            });

        /// <summary>
        ///     "Today, 2:02 PM · 12 min ago"
        /// </summary>
        public static readonly FuncValueConverter<ulong, string> ToFriendlyTime =
            new FuncValueConverter<ulong, string>(v =>
            {
                var local = DateTime.UnixEpoch.AddSeconds(v).ToLocalTime();
                var today = DateTime.Today;
                var time = local.ToString(ViewModels.Preferences.Instance.Use24Hours ? "HH:mm" : "h:mm tt");

                string day;
                if (local.Date == today)
                    day = App.Text("Date.Today");
                else if (local.Date == today.AddDays(-1))
                    day = App.Text("Date.Yesterday");
                else
                    day = local.ToString(local.Year == today.Year ? "MMM d" : "MMM d, yyyy");

                return $"{day}, {time} · {Models.RelativeTime.Format(local, true)}";
            });

        public static readonly FuncValueConverter<IBrush, IBrush> ToChipBackground =
            new FuncValueConverter<IBrush, IBrush>(v => v is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.14) : Brushes.Transparent);
    }
}
