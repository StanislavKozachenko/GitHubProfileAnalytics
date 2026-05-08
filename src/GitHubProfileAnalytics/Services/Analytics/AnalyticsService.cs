using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Octokit;

namespace GitHubProfileAnalytics.Services.Analytics;

public class AnalyticsService(IGitHubService gitHubService, IGitHubClient gitHubClient)
    : IAnalyticsService
{
    public async Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username)
    {
        GitHubProfileDto profile = await gitHubService.GetProfileAsync(username);

        int accountAgeDays = (int)(DateTimeOffset.UtcNow - profile.CreatedAt).TotalDays;

        double followersRatio =
            profile.Following > 0
                ? Math.Round((double)profile.Followers / profile.Following, 2)
                : profile.Followers;

        double reposPerYear =
            accountAgeDays > 0
                ? Math.Round((double)profile.PublicRepos / accountAgeDays * 365, 2)
                : 0;

        var profileMetrics = new ProfileMetrics
        {
            AccountAgeDays = accountAgeDays,
            FollowerRatio = followersRatio,
            ReposPerYear = reposPerYear,
        };

        IReadOnlyList<Repository> repos = await gitHubClient.Repository.GetAllForUser(
            username
        );

        int totalStars = repos.Sum(r => r.StargazersCount);
        int totalForks = repos.Sum(r => r.ForksCount);
        double averageStars =
            repos.Count > 0 ? Math.Round((double)totalStars / repos.Count, 2) : 0;

        var languages = repos
            .Where(r => r.Language != null)
            .GroupBy(r => r.Language)
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

        IReadOnlyList<Activity> events =
            await gitHubClient.Activity.Events.GetAllUserPerformed(username);

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
                DateTime date = e.CreatedAt.UtcDateTime;
                int daysFromMonday = (date.DayOfWeek - DayOfWeek.Monday + 7) % 7;
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
