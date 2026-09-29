using System;

namespace SourceGit.Models
{
    public static class RelativeTime
    {
        public static string Format(ulong timestamp, bool compact = false)
        {
            return Format(DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime(), compact);
        }

        public static string Format(DateTime localTime, bool compact = false)
        {
            var now = DateTime.Now;
            var span = now - localTime;
            return compact ? FormatCompact(now, localTime, span) : FormatNormal(now, localTime, span);
        }

        private static string FormatNormal(DateTime now, DateTime localTime, TimeSpan span)
        {
            if (span.TotalMinutes < 1)
                return App.Text("Period.JustNow");

            if (span.TotalHours < 1)
                return App.Text("Period.MinutesAgo", (int)span.TotalMinutes);

            if (span.TotalDays < 1)
            {
                var hours = (int)span.TotalHours;
                return hours == 1 ? App.Text("Period.HourAgo") : App.Text("Period.HoursAgo", hours);
            }

            var lastDay = now.AddDays(-1).Date;
            if (localTime >= lastDay)
                return App.Text("Period.Yesterday");

            if ((localTime.Year == now.Year && localTime.Month == now.Month) || span.TotalDays < 28)
            {
                var diffDay = now.Date - localTime.Date;
                return App.Text("Period.DaysAgo", (int)diffDay.TotalDays);
            }

            var lastMonth = now.AddMonths(-1).Date;
            if (localTime.Year == lastMonth.Year && localTime.Month == lastMonth.Month)
                return App.Text("Period.LastMonth");

            if (localTime.Year == now.Year || localTime > now.AddMonths(-11))
            {
                var diffMonth = (12 + now.Month - localTime.Month) % 12;
                return App.Text("Period.MonthsAgo", diffMonth);
            }

            var diffYear = now.Year - localTime.Year;
            if (diffYear == 1)
                return App.Text("Period.LastYear");

            return App.Text("Period.YearsAgo", diffYear);
        }

        private static string FormatCompact(DateTime now, DateTime localTime, TimeSpan span)
        {
            if (span.TotalMinutes < 1)
                return App.Text("Period.Compact.Now");

            if (span.TotalHours < 1)
                return App.Text("Period.Compact.Minutes", (int)span.TotalMinutes);

            if (span.TotalDays < 1)
                return App.Text("Period.Compact.Hours", (int)span.TotalHours);

            if (localTime >= now.AddDays(-1).Date)
                return App.Text("Period.Compact.Yesterday");

            if (span.TotalDays < 7)
                return App.Text("Period.Compact.Days", (int)(now.Date - localTime.Date).TotalDays);

            if (span.TotalDays < 30)
                return App.Text("Period.Compact.Weeks", (int)(span.TotalDays / 7));

            if (span.TotalDays < 365)
                return App.Text("Period.Compact.Months", Math.Max(1, (int)(span.TotalDays / 30)));

            return App.Text("Period.Compact.Years", (int)(span.TotalDays / 365));
        }
    }
}
