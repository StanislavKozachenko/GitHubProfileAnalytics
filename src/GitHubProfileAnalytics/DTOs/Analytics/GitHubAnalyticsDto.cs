namespace GitHubProfileAnalytics.DTOs.Analytics;

public class GitHubAnalyticsDto
{
    public ProfileMetrics Profile { get; set; } = new();
    public RepositoryMetrics Repositories { get; set; } = new();
    public ActivityMetrics Activity { get; set; } = new();
    public List<ContributionWeek> ContributionGraph { get; set; } = [];
}
