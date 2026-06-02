using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public class ComparisonService(IAnalyticsCacheService analyticsCacheService)
    : IComparisonService
{
    private const double StarsWeight = 0.25;
    private const double FollowerRatioWeight = 0.10;
    private const double ReposPerYearWeight = 0.15;
    private const double CommitsWeight = 0.25;
    private const double PrsAndReviewsWeight = 0.25;

    public async Task<ProfileComparisonDto> CompareAsync(
        string username1,
        string username2
    )
    {
        GitHubAnalyticsDto[] results = await Task.WhenAll(
            analyticsCacheService.GetAnalyticsAsync(username1),
            analyticsCacheService.GetAnalyticsAsync(username2)
        );

        GitHubAnalyticsDto first = results[0];
        GitHubAnalyticsDto second = results[1];

        return new ProfileComparisonDto(
            username1,
            first,
            CalculateScore(first, second),
            username2,
            second,
            CalculateScore(second, first)
        );
    }

    private static double CalculateScore(
        GitHubAnalyticsDto target,
        GitHubAnalyticsDto other
    )
    {
        double stars =
            Normalize(target.Repositories.TotalStars, other.Repositories.TotalStars)
            * StarsWeight;
        double followerRatio =
            Normalize(target.Profile.FollowerRatio, other.Profile.FollowerRatio)
            * FollowerRatioWeight;
        double reposPerYear =
            Normalize(target.Profile.ReposPerYear, other.Profile.ReposPerYear)
            * ReposPerYearWeight;
        double commits =
            Normalize(target.Activity.Commits, other.Activity.Commits) * CommitsWeight;
        double prsAndReviews =
            Normalize(
                target.Activity.PullRequests + target.Activity.Reviews,
                other.Activity.PullRequests + other.Activity.Reviews
            ) * PrsAndReviewsWeight;

        return Math.Round(
            (stars + followerRatio + reposPerYear + commits + prsAndReviews) * 100,
            1
        );
    }

    private static double Normalize(double value, double otherValue)
    {
        double max = Math.Max(value, otherValue);
        return max == 0 ? 0 : value / max;
    }
}
