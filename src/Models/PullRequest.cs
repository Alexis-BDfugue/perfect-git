using System;

namespace SourceGit.Models
{
    public enum PullRequestState
    {
        Open,
        Draft,
        Merged,
        Closed,
    }

    public class PullRequest
    {
        public int Number { get; set; } = 0;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int Comments { get; set; } = 0;
        public string Url { get; set; } = string.Empty;
        public PullRequestState State { get; set; } = PullRequestState.Open;

        /// <summary>GitHub repository ("owner/repo") the PR belongs to, and the local remote pointing to it.</summary>
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Remote { get; set; } = string.Empty;

        public string NumberLabel => $"#{Number}";

        public string StateLabel => State switch
        {
            PullRequestState.Draft => App.Text("PullRequest.State.Draft"),
            PullRequestState.Merged => App.Text("PullRequest.State.Merged"),
            PullRequestState.Closed => App.Text("PullRequest.State.Closed"),
            _ => App.Text("PullRequest.State.InReview"),
        };

        public string Details => Comments switch
        {
            0 => Author,
            1 => $"{Author} · {App.Text("PullRequest.Comment")}",
            _ => $"{Author} · {App.Text("PullRequest.Comments", Comments)}",
        };
    }

    /// <summary>
    ///     Details of one pull request (GitHub `pulls/{number}`), loaded when it is opened in the inspector.
    /// </summary>
    public class PullRequestInfo
    {
        public string Body { get; set; } = string.Empty;
        public string HeadRef { get; set; } = string.Empty;
        public string HeadSha { get; set; } = string.Empty;
        public string HeadRepo { get; set; } = string.Empty;
        public string BaseRef { get; set; } = string.Empty;
        public string BaseSha { get; set; } = string.Empty;
        public string BaseRepo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.MinValue;
        public DateTime? MergedAt { get; set; } = null;
        public DateTime? ClosedAt { get; set; } = null;
        public int Commits { get; set; } = 0;
        public int Additions { get; set; } = 0;
        public int Deletions { get; set; } = 0;
        public int ChangedFiles { get; set; } = 0;
        public int ReviewComments { get; set; } = 0;

        public bool IsFromFork => !string.IsNullOrEmpty(HeadRepo) && !HeadRepo.Equals(BaseRepo, StringComparison.OrdinalIgnoreCase);
    }
}
