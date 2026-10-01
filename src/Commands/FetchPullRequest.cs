namespace SourceGit.Commands
{
    /// <summary>
    ///     Fetches the commits of a GitHub pull request (`refs/pull/N/head`) and optionally another revision
    ///     (its base commit). Only FETCH_HEAD is updated: no local branch or remote-tracking ref is created.
    /// </summary>
    public class FetchPullRequest : Command
    {
        public FetchPullRequest(string repo, Models.Remote remote, int number, string extraRevision)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = remote.PrivateSSHKey;
            RaiseError = false;
            Args = $"fetch --no-tags {remote.Name} refs/pull/{number}/head {extraRevision}".TrimEnd();
        }
    }
}
