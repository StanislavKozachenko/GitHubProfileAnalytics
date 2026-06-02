using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using NSubstitute;

namespace GitHubProfileAnalytics.Tests;

public sealed class ComparisonServiceTests
{
    private static GitHubAnalyticsDto CreateAnalytics(
        int totalStars = 0,
        double followerRatio = 0,
        double reposPerYear = 0,
        int commits = 0,
        int pullRequests = 0,
        int reviews = 0
    )
    {
        return new GitHubAnalyticsDto(
            new ProfileMetrics(0, followerRatio, reposPerYear),
            new RepositoryMetrics(totalStars, 0, 0, []),
            new ActivityMetrics(0, commits, pullRequests, reviews, 0),
            []
        );
    }

    private static ComparisonService CreateSut(
        GitHubAnalyticsDto first,
        GitHubAnalyticsDto second
    )
    {
        IAnalyticsCacheService cache = Substitute.For<IAnalyticsCacheService>();
        _ = cache.GetAnalyticsAsync("user1").Returns(first);
        _ = cache.GetAnalyticsAsync("user2").Returns(second);
        return new ComparisonService(cache);
    }

    [Fact]
    public async Task BothUsersScoreMaxWhenMetricsAreEqual()
    {
        GitHubAnalyticsDto analytics = CreateAnalytics(
            totalStars: 100,
            followerRatio: 2.0,
            reposPerYear: 5.0,
            commits: 50,
            pullRequests: 10,
            reviews: 5
        );
        ComparisonService sut = CreateSut(analytics, analytics);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal(100.0, result.Profiles[0].Score);
        Assert.Equal(100.0, result.Profiles[1].Score);
    }

    [Fact]
    public async Task DominantUserScores100()
    {
        GitHubAnalyticsDto dominant = CreateAnalytics(
            totalStars: 1000,
            followerRatio: 5.0,
            reposPerYear: 10.0,
            commits: 200,
            pullRequests: 50,
            reviews: 30
        );
        GitHubAnalyticsDto other = CreateAnalytics();
        ComparisonService sut = CreateSut(dominant, other);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal(100.0, result.Profiles[0].Score);
        Assert.Equal(0.0, result.Profiles[1].Score);
    }

    [Fact]
    public async Task AllZeroMetricsProduceZeroScore()
    {
        GitHubAnalyticsDto analytics = CreateAnalytics();
        ComparisonService sut = CreateSut(analytics, analytics);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal(0.0, result.Profiles[0].Score);
        Assert.Equal(0.0, result.Profiles[1].Score);
    }

    [Fact]
    public async Task ScoreReflectsWeightedMetrics()
    {
        GitHubAnalyticsDto user1 = CreateAnalytics(totalStars: 1000);
        GitHubAnalyticsDto user2 = CreateAnalytics();
        ComparisonService sut = CreateSut(user1, user2);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal(25.0, result.Profiles[0].Score);
        Assert.Equal(0.0, result.Profiles[1].Score);
    }

    [Fact]
    public async Task ResultContainsCorrectUsernames()
    {
        GitHubAnalyticsDto analytics = CreateAnalytics();
        ComparisonService sut = CreateSut(analytics, analytics);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal("user1", result.Profiles[0].Username);
        Assert.Equal("user2", result.Profiles[1].Username);
    }
}
