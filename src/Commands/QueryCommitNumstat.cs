using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Added/deleted line counts per file between two revisions (binary files are skipped).
    /// </summary>
    public class QueryCommitNumstat : Command
    {
        public QueryCommitNumstat(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --no-color --no-ext-diff --numstat -z {based} {end}";
        }

        public async Task<Dictionary<string, Models.NumStat>> GetResultAsync()
        {
            var stats = new Dictionary<string, Models.NumStat>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return stats;

            // Format: "<add>\t<del>\t<path>\0", or for renames "<add>\t<del>\t\0<old>\0<new>\0".
            var parts = rs.StdOut.Split('\0');
            for (var i = 0; i < parts.Length; i++)
            {
                var fields = parts[i].Split('\t');
                if (fields.Length < 3)
                    continue;

                var path = fields[2];
                if (string.IsNullOrEmpty(path))
                {
                    if (i + 2 >= parts.Length)
                        break;

                    path = parts[i + 2];
                    i += 2;
                }

                if (int.TryParse(fields[0], out var added) && int.TryParse(fields[1], out var deleted))
                    stats[path] = new Models.NumStat(added, deleted);
            }

            return stats;
        }
    }
}
