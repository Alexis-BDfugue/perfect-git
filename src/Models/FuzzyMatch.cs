using System;

namespace SourceGit.Models
{
    public static class FuzzyMatch
    {
        /// <summary>
        ///     Scores how well `text` matches `query`: substring matches rank first, then in-order character
        ///     matches (consecutive characters and word starts are rewarded). Returns -1 when it doesn't match.
        /// </summary>
        public static int Score(string query, string text)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 1;

            if (string.IsNullOrEmpty(text))
                return -1;

            query = query.Trim();

            var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var wordStart = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                return 1000 + (wordStart ? 200 : 0) - idx * 2 - (text.Length - query.Length) / 4;
            }

            var score = 0;
            var ti = 0;
            var last = -2;
            foreach (var qc in query)
            {
                if (char.IsWhiteSpace(qc))
                    continue;

                var lower = char.ToLowerInvariant(qc);
                var found = false;
                while (ti < text.Length)
                {
                    if (char.ToLowerInvariant(text[ti]) == lower)
                    {
                        score += last == ti - 1 ? 15 : 5;
                        if (ti == 0 || !char.IsLetterOrDigit(text[ti - 1]))
                            score += 10;

                        last = ti;
                        ti++;
                        found = true;
                        break;
                    }

                    ti++;
                }

                if (!found)
                    return -1;
            }

            return score;
        }
    }
}
