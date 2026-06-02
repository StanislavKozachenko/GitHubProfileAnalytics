namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ProfileComparisonDto(
    string firstUsername,
    GitHubAnalyticsDto first,
    double firstScore,
    string secondUsername,
    GitHubAnalyticsDto second,
    double secondScore
)
{
    public string FirstUsername { get; init; } = firstUsername;
    public GitHubAnalyticsDto First { get; init; } = first;
    public double FirstScore { get; init; } = firstScore;
    public string SecondUsername { get; init; } = secondUsername;
    public GitHubAnalyticsDto Second { get; init; } = second;
    public double SecondScore { get; init; } = secondScore;
}
