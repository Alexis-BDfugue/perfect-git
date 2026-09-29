using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Dry-run merge (`git merge-tree`) that also lists the files that would conflict.
    /// </summary>
    public class MergeTreeConflicts : Command
    {
        public MergeTreeConflicts(string repo, string source, string dest)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"merge-tree --write-tree --name-only --no-messages {source} {dest}";
        }

        /// <returns>-1 when the test can not be run, otherwise the number of conflicting files.</returns>
        public async Task<int> GetConflictCountAsync()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
                var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (proc.ExitCode == 0)
                    return 0;

                if (proc.ExitCode != 1)
                    return -1;

                // First line is the tree OID, then the conflicting paths.
                var files = new HashSet<string>();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 1; i < lines.Length; i++)
                    files.Add(lines[i].Trim());

                return Math.Max(1, files.Count);
            }
            catch
            {
                return -1;
            }
        }
    }
}
