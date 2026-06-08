using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
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
        IAnalyticsCacheService analyticsCache = Substitute.For<IAnalyticsCacheService>();
        _ = analyticsCache.GetAnalyticsAsync("user1").Returns(first);
        _ = analyticsCache.GetAnalyticsAsync("user2").Returns(second);
        IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        IConfiguration configuration = new ConfigurationBuilder().Build();
        return new ComparisonService(analyticsCache, memoryCache, configuration);
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

    [Fact]
    public async Task WinnerIsNullWhenScoresAreEqual()
    {
        GitHubAnalyticsDto analytics = CreateAnalytics(totalStars: 100, commits: 50);
        ComparisonService sut = CreateSut(analytics, analytics);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Null(result.Winner);
    }

    [Fact]
    public async Task WinnerIsFirstUserWhenFirstDominates()
    {
        GitHubAnalyticsDto dominant = CreateAnalytics(totalStars: 1000, commits: 200);
        GitHubAnalyticsDto weak = CreateAnalytics();
        ComparisonService sut = CreateSut(dominant, weak);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal("user1", result.Winner);
    }

    [Fact]
    public async Task WinnerIsSecondUserWhenSecondDominates()
    {
        GitHubAnalyticsDto weak = CreateAnalytics();
        GitHubAnalyticsDto dominant = CreateAnalytics(totalStars: 1000, commits: 200);
        ComparisonService sut = CreateSut(weak, dominant);

        ProfileComparisonDto result = await sut.CompareAsync("user1", "user2");

        Assert.Equal("user2", result.Winner);
    }
}
