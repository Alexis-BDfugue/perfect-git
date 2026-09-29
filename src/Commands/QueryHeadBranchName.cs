using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryHeadBranchName : Command
    {
        public QueryHeadBranchName(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = "symbolic-ref --short -q HEAD";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }
    }
}
