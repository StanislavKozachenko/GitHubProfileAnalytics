using GitHubProfileAnalytics.DTOs.Analytics;
using Microsoft.Extensions.Caching.Memory;

namespace GitHubProfileAnalytics.Services.Analytics;

public class ComparisonService(
    IAnalyticsCacheService analyticsCacheService,
    IMemoryCache cache,
    IConfiguration configuration
) : IComparisonService
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
        string cacheKey = $"comparison:{username1}:{username2}";
        if (cache.TryGetValue(cacheKey, out ProfileComparisonDto? cached))
        {
            return cached!;
        }

        GitHubAnalyticsDto first = await analyticsCacheService.GetAnalyticsAsync(
            username1
        );
        GitHubAnalyticsDto second = await analyticsCacheService.GetAnalyticsAsync(
            username2
        );

        ProfileComparisonDto result = new([
            new ComparisonEntryDto(
                username1,
                CalculateScore(first, second),
                first.Profile,
                first.Repositories,
                first.Activity,
                first.ContributionGraph
            ),
            new ComparisonEntryDto(
                username2,
                CalculateScore(second, first),
                second.Profile,
                second.Repositories,
                second.Activity,
                second.ContributionGraph
            ),
        ]);

        int ttlHours = configuration.GetValue("ComparisonCache:TtlHours", 1);
        _ = cache.Set(cacheKey, result, TimeSpan.FromHours(ttlHours));
        return result;
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
