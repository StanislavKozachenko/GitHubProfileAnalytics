using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GitHubProfileAnalytics.Tests;

public sealed class AnalyticsCacheServiceTests(DatabaseFixture fixture)
    : IClassFixture<DatabaseFixture>,
        IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.TruncateAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    private AppDbContext CreateDbContext()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options
        );
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
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();
        IConfiguration config = CreateConfiguration();

        var cached = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow,
        };

        _ = db.AnalyticsCaches.Add(cached);
        _ = await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        GitHubAnalyticsDto result = await sut.GetAnalyticsAsync("testuser");

        Assert.NotNull(result);
        _ = await analyticsService.DidNotReceive().GetAnalyticsAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task FetchesFromAnalyticsServiceAndCreatesEntryWhenCacheEmpty()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();
        IConfiguration config = CreateConfiguration();
        var analytics = new GitHubAnalyticsDto();

        _ = analyticsService.GetAnalyticsAsync("testuser").Returns(analytics);

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        _ = await sut.GetAnalyticsAsync("testuser");

        AnalyticsCache entry = db.AnalyticsCaches.Single();
        Assert.Equal("testuser", entry.GitHubUserName);
        Assert.Equal(JsonSerializer.Serialize(analytics), entry.Data);
    }

    [Fact]
    public async Task FetchesFromAnalyticsServiceAndUpdatesEntryWhenCacheExpired()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();
        IConfiguration config = CreateConfiguration(ttlHours: 1);

        var expired = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        _ = db.AnalyticsCaches.Add(expired);
        _ = await db.SaveChangesAsync();

        _ = analyticsService
            .GetAnalyticsAsync("testuser")
            .Returns(new GitHubAnalyticsDto());

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        _ = await sut.GetAnalyticsAsync("testuser");

        Assert.Equal(1, db.AnalyticsCaches.Count());
        _ = await analyticsService.Received(1).GetAnalyticsAsync("testuser");
    }

    [Fact]
    public async Task UpdatesCachedAtWhenCacheExpired()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();
        IConfiguration config = CreateConfiguration(ttlHours: 1);

        var expired = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        _ = db.AnalyticsCaches.Add(expired);
        _ = await db.SaveChangesAsync();

        DateTimeOffset oldCachedAt = expired.CachedAt;

        _ = analyticsService
            .GetAnalyticsAsync("testuser")
            .Returns(new GitHubAnalyticsDto());

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        _ = await sut.GetAnalyticsAsync("testuser");

        AnalyticsCache updatedEntry = db.AnalyticsCaches.Single(p =>
            p.GitHubUserName == "testuser"
        );
        Assert.True(updatedEntry.CachedAt > oldCachedAt);
    }

    [Fact]
    public async Task ThrowsWhenCachedDataIsCorrupted()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();
        IConfiguration config = CreateConfiguration();

        var cached = new AnalyticsCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = "corruptedstring",
            CachedAt = DateTimeOffset.UtcNow,
        };

        _ = db.AnalyticsCaches.Add(cached);
        _ = await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, config);

        _ = await Assert.ThrowsAsync<JsonException>(async () =>
            await sut.GetAnalyticsAsync("testuser")
        );
    }

    [Fact]
    public async Task ThrowsInvalidOperationWithUsernameWhenDeserializesNull()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();

        _ = db.AnalyticsCaches.Add(
            new AnalyticsCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = "null",
                CachedAt = DateTimeOffset.UtcNow,
            }
        );
        _ = await db.SaveChangesAsync();

        var sut = new AnalyticsCacheService(db, analyticsService, CreateConfiguration());

        InvalidOperationException ex =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAnalyticsAsync("testuser")
            );
        Assert.Contains("testuser", ex.Message);
    }

    [Fact]
    public async Task UsesTtlKeyFromConfigurationForFreshnessCheck()
    {
        AppDbContext db = CreateDbContext();
        IAnalyticsService analyticsService = Substitute.For<IAnalyticsService>();

        _ = db.AnalyticsCaches.Add(
            new AnalyticsCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = JsonSerializer.Serialize(new GitHubAnalyticsDto()),
                CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
            }
        );
        _ = await db.SaveChangesAsync();

        // TTL=24h → 2h-old entry is fresh → no service call
        var sut = new AnalyticsCacheService(
            db,
            analyticsService,
            CreateConfiguration(ttlHours: 24)
        );
        _ = await sut.GetAnalyticsAsync("testuser");

        _ = await analyticsService.DidNotReceive().GetAnalyticsAsync(Arg.Any<string>());
    }
}
