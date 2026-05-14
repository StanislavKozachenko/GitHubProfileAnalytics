namespace GitHubProfileAnalytics.DTOs.Analytics;

public class GitHubAnalyticsDto(
    ProfileMetrics profile,
    RepositoryMetrics repositories,
    ActivityMetrics activity,
    List<ContributionWeek> contributionGraph
)
{
    public ProfileMetrics Profile { get; init; } = profile;
    public RepositoryMetrics Repositories { get; init; } = repositories;
    public ActivityMetrics Activity { get; init; } = activity;
    public List<ContributionWeek> ContributionGraph { get; init; } = contributionGraph;
}
