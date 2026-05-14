namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ContributionWeek(DateOnly week, int count)
{
    public DateOnly Week { get; init; } = week;
    public int Count { get; init; } = count;
}
