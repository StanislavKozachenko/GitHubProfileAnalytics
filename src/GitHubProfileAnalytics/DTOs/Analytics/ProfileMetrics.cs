namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ProfileMetrics(int accountAgeDays, double followerRatio, double reposPerYear)
{
    public int AccountAgeDays { get; init; } = accountAgeDays;
    public double FollowerRatio { get; init; } = followerRatio;
    public double ReposPerYear { get; init; } = reposPerYear;
}
