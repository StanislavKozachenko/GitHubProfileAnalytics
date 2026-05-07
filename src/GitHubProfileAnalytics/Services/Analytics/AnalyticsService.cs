using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.GitHub;
using Octokit;

namespace GitHubProfileAnalytics.Services.Analytics;

public class AnalyticsService(IGitHubService gitHubService, IGitHubClient gitHubClient)
    : IAnalyticsService
{
    public async Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username)
    {
        var profile = await gitHubService.GetProfileAsync(username);

        var accountAgeDays = (int)(DateTimeOffset.UtcNow - profile.CreatedAt).TotalDays;

        var followersRatio =
            profile.Following > 0
                ? Math.Round((double)profile.Followers / profile.Following, 2)
                : profile.Followers;

        var reposPerYear =
            accountAgeDays > 0
                ? Math.Round((double)profile.PublicRepos / accountAgeDays * 365, 2)
                : 0;

        var profileMetrics = new ProfileMetrics
        {
            AccountAgeDays = accountAgeDays,
            FollowerRatio = followersRatio,
            ReposPerYear = reposPerYear,
        };

        var repos = await gitHubClient.Repository.GetAllForUser(username);

        var totalStars = repos.Sum(r => r.StargazersCount);
        var totalForks = repos.Sum(r => r.ForksCount);
        var averageStars = repos.Count > 0 ? Math.Round((double)totalStars / repos.Count, 2) : 0;

        var languages = repos
            .Where(r => r.Language != null)
            .GroupBy(r => r.Language!)
            .Select(g => new LanguageStat
            {
                Name = g.Key,
                Percent = Math.Round((double)g.Count() / repos.Count * 100, 1),
            })
            .OrderByDescending(l => l.Percent)
            .Take(5)
            .ToList();

        var repositoryMetrics = new RepositoryMetrics
        {
            TotalStars = totalStars,
            TotalForks = totalForks,
            AverageStarsPerRepo = averageStars,
            TopLanguages = languages,
        };

        var events = await gitHubClient.Activity.Events.GetAllUserPerformed(username);

        var activityMetrics = new ActivityMetrics
        {
            TotalEvents = events.Count,
            Commits = events
                .Where(e => e.Type == "PushEvent")
                .Sum(e => e.Payload is PushEventPayload p ? p.Commits?.Count ?? 0 : 0),
            PullRequests = events.Count(e => e.Type == "PullRequestEvent"),
            Reviews = events.Count(e => e.Type == "PullRequestReviewEvent"),
            Issues = events.Count(e => e.Type == "IssuesEvent"),
        };

        var contributionGraph = events
            .GroupBy(e =>
            {
                var date = e.CreatedAt.UtcDateTime;
                var daysFromMonday = (date.DayOfWeek - DayOfWeek.Monday + 7) % 7;
                return DateOnly.FromDateTime(date.AddDays(-daysFromMonday));
            })
            .Select(g => new ContributionWeek { Week = g.Key, Count = g.Count() })
            .OrderBy(w => w.Week)
            .ToList();

        return new GitHubAnalyticsDto
        {
            Profile = profileMetrics,
            Repositories = repositoryMetrics,
            Activity = activityMetrics,
            ContributionGraph = contributionGraph,
        };
    }
}
