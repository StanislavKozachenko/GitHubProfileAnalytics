using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GitHubProfileAnalytics.Tests;

public sealed class ProfileCacheServiceTests
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
                new Dictionary<string, string?> { ["ProfileCache:TtlHours"] = ttlHours.ToString() }
            )
            .Build();
    }

    [Fact]
    public async Task ReturnsCachedProfileWithoutCallingGitHub()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration();

        var cached = new ProfileCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubProfileDto { Login = "testuser" }),
            CachedAt = DateTimeOffset.UtcNow,
        };

        db.ProfileCaches.Add(cached);
        await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        var result = await sut.GetProfileAsync("testuser");

        Assert.Equal("testuser", result.Login);
        await gitHubService.DidNotReceive().GetProfileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task FetchesFromGitHubAndCreatesEntryWhenCacheEmpty()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration();
        var profile = new GitHubProfileDto { Login = "testuser", Name = "Test User", Followers = 7 };

        gitHubService.GetProfileAsync("testuser").Returns(profile);

        var sut = new ProfileCacheService(db, gitHubService, config);

        var result = await sut.GetProfileAsync("testuser");

        Assert.Equal("testuser", result.Login);
        var entry = db.ProfileCaches.Single();
        Assert.Equal("testuser", entry.GitHubUserName);
        Assert.Equal(JsonSerializer.Serialize(profile), entry.Data);
    }

    [Fact]
    public async Task FetchesFromGitHubAndUpdatesEntryWhenCacheExpired()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration(ttlHours: 1);

        var expired = new ProfileCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubProfileDto { Login = "testuser" }),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        db.ProfileCaches.Add(expired);
        await db.SaveChangesAsync();

        gitHubService
            .GetProfileAsync("testuser")
            .Returns(new GitHubProfileDto { Login = "testuser" });

        var sut = new ProfileCacheService(db, gitHubService, config);

        await sut.GetProfileAsync("testuser");

        Assert.Equal(1, db.ProfileCaches.Count());
        await gitHubService.Received(1).GetProfileAsync("testuser");
    }

    [Fact]
    public async Task UpdatesCachedAtWhenCacheExpired()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration(ttlHours: 1);

        var expired = new ProfileCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = JsonSerializer.Serialize(new GitHubProfileDto { Login = "testuser" }),
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };

        db.ProfileCaches.Add(expired);
        await db.SaveChangesAsync();

        var oldCachedAt = expired.CachedAt;

        gitHubService
            .GetProfileAsync("testuser")
            .Returns(new GitHubProfileDto { Login = "testuser" });

        var sut = new ProfileCacheService(db, gitHubService, config);

        await sut.GetProfileAsync("testuser");

        var updatedEntry = db.ProfileCaches.Single(p => p.GitHubUserName == "testuser");
        Assert.True(updatedEntry.CachedAt > oldCachedAt);
    }

    [Fact]
    public async Task ThrowsWhenCachedDataIsCorrupted()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration();

        var cached = new ProfileCache
        {
            Id = Guid.NewGuid(),
            GitHubUserName = "testuser",
            Data = "corruptedstring",
            CachedAt = DateTimeOffset.UtcNow,
        };

        db.ProfileCaches.Add(cached);
        await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        await Assert.ThrowsAsync<JsonException>(async () => await sut.GetProfileAsync("testuser"));
    }

    [Fact]
    public async Task ThrowsInvalidOperationWithUsernameWhenDeserializesNull()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration();

        db.ProfileCaches.Add(
            new ProfileCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = "null",
                CachedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetProfileAsync("testuser")
        );
        Assert.Contains("testuser", ex.Message);
    }

    [Fact]
    public async Task ReturnsCachedDataWithAllFieldsMapped()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();
        var config = CreateConfiguration();

        var profile = new GitHubProfileDto
        {
            Login = "testuser",
            Name = "Test User",
            Followers = 100,
            Following = 50,
            PublicRepos = 10,
        };

        db.ProfileCaches.Add(
            new ProfileCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = JsonSerializer.Serialize(profile),
                CachedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var sut = new ProfileCacheService(db, gitHubService, config);

        var result = await sut.GetProfileAsync("testuser");

        Assert.Equal("Test User", result.Name);
        Assert.Equal(100, result.Followers);
        Assert.Equal(50, result.Following);
        Assert.Equal(10, result.PublicRepos);
    }

    [Fact]
    public async Task UsesTtlKeyFromConfigurationForFreshnessCheck()
    {
        var db = CreateDbContext();
        var gitHubService = Substitute.For<IGitHubService>();

        // Entry is 2h old — fresh only if TTL >= 2h
        db.ProfileCaches.Add(
            new ProfileCache
            {
                Id = Guid.NewGuid(),
                GitHubUserName = "testuser",
                Data = JsonSerializer.Serialize(new GitHubProfileDto { Login = "testuser", Name = "Cached" }),
                CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
            }
        );
        await db.SaveChangesAsync();

        // TTL=24h → entry is fresh → no API call
        var sut = new ProfileCacheService(db, gitHubService, CreateConfiguration(ttlHours: 24));
        var result = await sut.GetProfileAsync("testuser");

        Assert.Equal("Cached", result.Name);
        await gitHubService.DidNotReceive().GetProfileAsync(Arg.Any<string>());
    }
}
