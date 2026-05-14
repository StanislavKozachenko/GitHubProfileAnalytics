using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GitHubProfileAnalytics.Tests;

public sealed class ProfileCacheServiceTests(DatabaseFixture fixture)
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
        return new(
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
                    ["ProfileCache:TtlHours"] = ttlHours.ToString(),
                }
            )
            .Build();
    }

    private static GitHubProfileDto ProfileDto(
        string login = "",
        string name = "",
        int followers = 0,
        int following = 0,
        int publicRepos = 0
    )
    {
        return new(
            login,
            name,
            string.Empty,
            string.Empty,
            publicRepos,
            followers,
            following,
            default
        );
    }

    [Fact]
    public async Task ReturnsCachedProfileWithoutCallingGitHub()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration();

        var cached = new ProfileCache(
            Guid.NewGuid(),
            "testuser",
            JsonSerializer.Serialize(ProfileDto(login: "testuser")),
            DateTimeOffset.UtcNow
        );

        _ = db.ProfileCaches.Add(cached);
        _ = await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        GitHubProfileDto result = await sut.GetProfileAsync("testuser");

        Assert.Equal("testuser", result.Login);
        _ = await gitHubService.DidNotReceive().GetProfileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task FetchesFromGitHubAndCreatesEntryWhenCacheEmpty()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration();
        GitHubProfileDto profile = ProfileDto(
            login: "testuser",
            name: "Test User",
            followers: 7
        );

        _ = gitHubService.GetProfileAsync("testuser").Returns(profile);

        var sut = new ProfileCacheService(db, gitHubService, config);

        GitHubProfileDto result = await sut.GetProfileAsync("testuser");

        Assert.Equal("testuser", result.Login);
        ProfileCache entry = db.ProfileCaches.Single();
        Assert.Equal("testuser", entry.GitHubUserName);
        Assert.Equal(JsonSerializer.Serialize(profile), entry.Data);
    }

    [Fact]
    public async Task FetchesFromGitHubAndUpdatesEntryWhenCacheExpired()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration(ttlHours: 1);

        _ = db.ProfileCaches.Add(
            new ProfileCache(
                Guid.NewGuid(),
                "testuser",
                JsonSerializer.Serialize(ProfileDto(login: "testuser")),
                DateTimeOffset.UtcNow.AddHours(-2)
            )
        );
        _ = await db.SaveChangesAsync();

        _ = gitHubService
            .GetProfileAsync("testuser")
            .Returns(ProfileDto(login: "testuser"));

        var sut = new ProfileCacheService(db, gitHubService, config);

        _ = await sut.GetProfileAsync("testuser");

        Assert.Equal(1, db.ProfileCaches.Count());
        _ = await gitHubService.Received(1).GetProfileAsync("testuser");
    }

    [Fact]
    public async Task UpdatesCachedAtWhenCacheExpired()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration(ttlHours: 1);

        var expired = new ProfileCache(
            Guid.NewGuid(),
            "testuser",
            JsonSerializer.Serialize(ProfileDto(login: "testuser")),
            DateTimeOffset.UtcNow.AddHours(-2)
        );

        _ = db.ProfileCaches.Add(expired);
        _ = await db.SaveChangesAsync();

        DateTimeOffset oldCachedAt = expired.CachedAt;

        _ = gitHubService
            .GetProfileAsync("testuser")
            .Returns(ProfileDto(login: "testuser"));

        var sut = new ProfileCacheService(db, gitHubService, config);

        _ = await sut.GetProfileAsync("testuser");

        ProfileCache updatedEntry = db.ProfileCaches.Single(p =>
            p.GitHubUserName == "testuser"
        );
        Assert.True(updatedEntry.CachedAt > oldCachedAt);
    }

    [Fact]
    public async Task ThrowsWhenCachedDataIsCorrupted()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration();

        var cached = new ProfileCache(
            Guid.NewGuid(),
            "testuser",
            "corruptedstring",
            DateTimeOffset.UtcNow
        );

        _ = db.ProfileCaches.Add(cached);
        _ = await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        _ = await Assert.ThrowsAsync<JsonException>(async () =>
            await sut.GetProfileAsync("testuser")
        );
    }

    [Fact]
    public async Task ThrowsInvalidOperationWithUsernameWhenDeserializesNull()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration();

        _ = db.ProfileCaches.Add(
            new ProfileCache(Guid.NewGuid(), "testuser", "null", DateTimeOffset.UtcNow)
        );
        _ = await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        InvalidOperationException ex =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetProfileAsync("testuser")
            );
        Assert.Contains("testuser", ex.Message);
    }

    [Fact]
    public async Task ReturnsCachedDataWithAllFieldsMapped()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();
        IConfiguration config = CreateConfiguration();

        GitHubProfileDto profile = ProfileDto(
            login: "testuser",
            name: "Test User",
            followers: 100,
            following: 50,
            publicRepos: 10
        );

        _ = db.ProfileCaches.Add(
            new ProfileCache(
                Guid.NewGuid(),
                "testuser",
                JsonSerializer.Serialize(profile),
                DateTimeOffset.UtcNow
            )
        );
        _ = await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        GitHubProfileDto result = await sut.GetProfileAsync("testuser");

        Assert.Equal("Test User", result.Name);
        Assert.Equal(100, result.Followers);
        Assert.Equal(50, result.Following);
        Assert.Equal(10, result.PublicRepos);
    }

    [Fact]
    public async Task UsesTtlKeyFromConfigurationForFreshnessCheck()
    {
        AppDbContext db = CreateDbContext();
        IGitHubService gitHubService = Substitute.For<IGitHubService>();

        // Entry is 2h old — fresh only if TTL >= 2h
        _ = db.ProfileCaches.Add(
            new ProfileCache(
                Guid.NewGuid(),
                "testuser",
                JsonSerializer.Serialize(ProfileDto(login: "testuser", name: "Cached")),
                DateTimeOffset.UtcNow.AddHours(-2)
            )
        );
        _ = await db.SaveChangesAsync();

        // TTL=24h → entry is fresh → no API call
        var sut = new ProfileCacheService(
            db,
            gitHubService,
            CreateConfiguration(ttlHours: 24)
        );
        GitHubProfileDto result = await sut.GetProfileAsync("testuser");

        Assert.Equal("Cached", result.Name);
        _ = await gitHubService.DidNotReceive().GetProfileAsync(Arg.Any<string>());
    }
}
