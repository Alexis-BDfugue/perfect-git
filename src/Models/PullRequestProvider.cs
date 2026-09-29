using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Reads recent pull requests from GitHub. Uses the `gh` CLI when available (so private
    ///     repositories work with the user's credentials), otherwise the public REST API.
    /// </summary>
    public static partial class PullRequestProvider
    {
        [GeneratedRegex(@"github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?/?$")]
        private static partial Regex REG_GITHUB();

        public static bool TryParseGitHub(string url, out string owner, out string repo)
        {
            owner = null;
            repo = null;

            if (string.IsNullOrEmpty(url))
                return false;

            var match = REG_GITHUB().Match(url.Trim());
            if (!match.Success)
                return false;

            owner = match.Groups[1].Value;
            repo = match.Groups[2].Value;
            return true;
        }

        public static async Task<List<PullRequest>> FetchAsync(List<Remote> remotes)
        {
            var ordered = new List<Remote>(remotes);
            ordered.Sort((l, r) => (l.Name == "origin" ? 0 : 1) - (r.Name == "origin" ? 0 : 1));

            foreach (var remote in ordered)
            {
                if (!TryParseGitHub(remote.URL, out var owner, out var repo))
                    continue;

                var path = $"repos/{owner}/{repo}/issues?state=all&sort=updated&direction=desc&per_page=30";
                var json = await QueryWithGhAsync(path).ConfigureAwait(false) ??
                           await QueryWithHttpAsync(path).ConfigureAwait(false);
                if (json != null)
                    return Parse(json);
            }

            return [];
        }

        private static async Task<string> QueryWithGhAsync(string path)
        {
            try
            {
                var start = new ProcessStartInfo("gh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                start.ArgumentList.Add("api");
                start.ArgumentList.Add(path);

                using var proc = Process.Start(start);
                if (proc == null)
                    return null;

                var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
                return proc.ExitCode == 0 ? output : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> QueryWithHttpAsync(string path)
        {
            try
            {
                using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PerfectGit");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                using var rsp = await client.GetAsync($"https://api.github.com/{path}").ConfigureAwait(false);
                if (!rsp.IsSuccessStatusCode)
                    return null;

                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private static List<PullRequest> Parse(string json)
        {
            var prs = new List<PullRequest>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return prs;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("pull_request", out var pr))
                        continue;

                    var state = PullRequestState.Open;
                    if (item.GetProperty("state").GetString() != "open")
                        state = pr.TryGetProperty("merged_at", out var mergedAt) && mergedAt.ValueKind == JsonValueKind.String
                            ? PullRequestState.Merged
                            : PullRequestState.Closed;
                    else if (item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                        state = PullRequestState.Draft;

                    prs.Add(new PullRequest()
                    {
                        Number = item.GetProperty("number").GetInt32(),
                        Title = item.GetProperty("title").GetString() ?? string.Empty,
                        Author = item.GetProperty("user").GetProperty("login").GetString() ?? string.Empty,
                        Comments = item.TryGetProperty("comments", out var comments) ? comments.GetInt32() : 0,
                        Url = item.GetProperty("html_url").GetString() ?? string.Empty,
                        State = state,
                    });

                    if (prs.Count >= 8)
                        break;
                }
            }
            catch
            {
                // Unexpected payload: just show nothing.
            }

            return prs;
        }
    }
}
