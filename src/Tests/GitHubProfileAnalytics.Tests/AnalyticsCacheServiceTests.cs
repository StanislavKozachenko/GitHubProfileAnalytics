using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GitHubProfileAnalytics.Tests;

public sealed class AnalyticsCacheServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfiguration(int ttlHours = 1)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AnalyticsCache:TtlHours"] = ttlHours.ToString(),
                }
            )
            .Build();
    }

    [Fact]
    public async Task ReturnsCachedAnalyticsWithoutCallingAnalyticsService()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();
        var config = CreateConfiguration();

        var cached = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow,
        };

        db.AnalyticsCaches.Add(cached);
        await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        var result = await sut.GetAnalyticsAsync("testuser");

        Assert.NotNull(result);
        await analyticsService.DidNotReceive().GetAnalyticsAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task FetchesFromAnalyticsServiceAndCreatesEntryWhenCacheEmpty()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();
        var config = CreateConfiguration();
        var analytics = new GitHubAnalyticsDto();

        analyticsService.GetAnalyticsAsync("testuser").Returns(analytics);

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        await sut.GetAnalyticsAsync("testuser");

        var entry = db.AnalyticsCaches.Single();
        Assert.Equal("testuser", entry.GitHubUserName);
        Assert.Equal(JsonSerializer.Serialize(analytics), entry.Data);
    }

    [Fact]
    public async Task FetchesFromAnalyticsServiceAndUpdatesEntryWhenCacheExpired()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();
        var config = CreateConfiguration(ttlHours: 1);

        var expired = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        db.AnalyticsCaches.Add(expired);
        await db.SaveChangesAsync();

        analyticsService.GetAnalyticsAsync("testuser").Returns(new GitHubAnalyticsDto());

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        await sut.GetAnalyticsAsync("testuser");

        Assert.Equal(1, db.AnalyticsCaches.Count());
        await analyticsService.Received(1).GetAnalyticsAsync("testuser");
    }

    [Fact]
    public async Task UpdatesCachedAtWhenCacheExpired()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();
        var config = CreateConfiguration(ttlHours: 1);

        var expired = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        db.AnalyticsCaches.Add(expired);
        await db.SaveChangesAsync();

        var oldCachedAt = expired.CachedAt;

        analyticsService.GetAnalyticsAsync("testuser").Returns(new GitHubAnalyticsDto());

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        await sut.GetAnalyticsAsync("testuser");

        var updatedEntry = db.AnalyticsCaches.Single(p => p.GitHubUserName == "testuser");
        Assert.True(updatedEntry.CachedAt > oldCachedAt);
    }

    [Fact]
    public async Task ThrowsWhenCachedDataIsCorrupted()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();
        var config = CreateConfiguration();

        var cached = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = "corruptedstring",
            CachedAt = DateTimeOffset.UtcNow,
        };

        db.AnalyticsCaches.Add(cached);
        await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        await Assert.ThrowsAsync<JsonException>(async () =>
            await sut.GetAnalyticsAsync("testuser")
        );
    }

    [Fact]
    public async Task ThrowsInvalidOperationWithUsernameWhenDeserializesNull()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();

        db.AnalyticsCaches.Add(
            new AnalyticsCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = "null",
                CachedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, CreateConfiguration());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAnalyticsAsync("testuser")
        );
        Assert.Contains("testuser", ex.Message);
    }

    [Fact]
    public async Task UsesTtlKeyFromConfigurationForFreshnessCheck()
    {
        var db = CreateDbContext();
        var analyticsService = Substitute.For<IAnalyticsService>();

        db.AnalyticsCaches.Add(
            new AnalyticsCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
                CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
            }
        );
        await db.SaveChangesAsync();

        // TTL=24h → 2h-old entry is fresh → no service call
        var sut = new AnalyticsCacheService(
            db,
            analyticsService,
            CreateConfiguration(ttlHours: 24)
        );
        await sut.GetAnalyticsAsync("testuser");

        await analyticsService.DidNotReceive().GetAnalyticsAsync(Arg.Any<string>());
    }
}
