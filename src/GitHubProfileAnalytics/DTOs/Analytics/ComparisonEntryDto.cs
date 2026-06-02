namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ComparisonEntryDto(
    string username,
    double score,
    ProfileMetrics profile,
    RepositoryMetrics repositories,
    ActivityMetrics activity,
    List<ContributionWeek> contributionGraph
)
{
    public string Username { get; init; } = username;
    public double Score { get; init; } = score;
    public ProfileMetrics Profile { get; init; } = profile;
    public RepositoryMetrics Repositories { get; init; } = repositories;
    public ActivityMetrics Activity { get; init; } = activity;
    public List<ContributionWeek> ContributionGraph { get; init; } = contributionGraph;
}
