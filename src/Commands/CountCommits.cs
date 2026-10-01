using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class CountCommits : Command
    {
        public CountCommits(string repo, string range)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"rev-list --count {range}";
        }

        public async Task<int> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess && int.TryParse(rs.StdOut.Trim(), out var count) ? count : -1;
        }
    }
}
