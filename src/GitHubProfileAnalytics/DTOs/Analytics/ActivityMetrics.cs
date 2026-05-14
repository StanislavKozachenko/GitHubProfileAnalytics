namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ActivityMetrics(
    int totalEvents,
    int commits,
    int pullRequests,
    int reviews,
    int issues
)
{
    public int TotalEvents { get; init; } = totalEvents;
    public int Commits { get; init; } = commits;
    public int PullRequests { get; init; } = pullRequests;
    public int Reviews { get; init; } = reviews;
    public int Issues { get; init; } = issues;
}
