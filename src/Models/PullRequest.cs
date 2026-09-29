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
}
