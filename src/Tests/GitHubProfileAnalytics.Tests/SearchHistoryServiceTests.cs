using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Tests;

public sealed class SearchHistoryServiceTests(DatabaseFixture fixture)
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

    [Fact]
    public async Task AddAsyncSavesSearchHistoryToDatabase()
    {
        AppDbContext db = CreateDbContext();

        var sut = new SearchHistoryService(db);

        Guid userId = Guid.NewGuid();
        await sut.AddAsync(userId, "test");

        Domain.SearchHistory entry = db.SearchHistories.Single();
        Assert.Equal("test", entry.GitHubUserName);
        Assert.Equal(userId, entry.UserId);
        Assert.True(entry.SearchedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task GetHistoryAsyncReturnsOnlyHistoryForRequestingUser()
    {
        AppDbContext db = CreateDbContext();

        var sut = new SearchHistoryService(db);

        Guid userId = Guid.NewGuid();
        await sut.AddAsync(userId, "test");
        await sut.AddAsync(Guid.NewGuid(), "other");

        IReadOnlyList<SearchHistoryItemDto> result = await sut.GetHistoryAsync(
            userId,
            100
        );
        _ = Assert.Single(result);
        Assert.Equal("test", result[0].GitHubUserName);
    }

    [Fact]
    public async Task GetHistoryAsyncReturnsHistoryOrderedBySearchedAtDesc()
    {
        AppDbContext db = CreateDbContext();

        var sut = new SearchHistoryService(db);

        Guid userId = Guid.NewGuid();
        db.SearchHistories.AddRange(
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "first",
                DateTimeOffset.UtcNow.AddHours(-1)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "second",
                DateTimeOffset.UtcNow.AddHours(-2)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "third",
                DateTimeOffset.UtcNow.AddHours(-3)
            )
        );
        _ = await db.SaveChangesAsync();

        IReadOnlyList<SearchHistoryItemDto> result = await sut.GetHistoryAsync(
            userId,
            100
        );

        Assert.Equal(3, result.Count);
        Assert.True(result[0].SearchedAt > result[1].SearchedAt);
        Assert.True(result[1].SearchedAt > result[2].SearchedAt);
    }

    [Fact]
    public async Task GetHistoryAsyncRespectsLimit()
    {
        AppDbContext db = CreateDbContext();

        var sut = new SearchHistoryService(db);

        Guid userId = Guid.NewGuid();
        db.SearchHistories.AddRange(
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "first",
                DateTimeOffset.UtcNow.AddHours(-1)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "second",
                DateTimeOffset.UtcNow.AddHours(-2)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "third",
                DateTimeOffset.UtcNow.AddHours(-3)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "fourth",
                DateTimeOffset.UtcNow.AddHours(-4)
            ),
            new Domain.SearchHistory(
                Guid.NewGuid(),
                userId,
                "fifth",
                DateTimeOffset.UtcNow.AddHours(-5)
            )
        );
        _ = await db.SaveChangesAsync();

        IReadOnlyList<SearchHistoryItemDto> result = await sut.GetHistoryAsync(userId, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetHistoryAsyncReturnsEmptyListWhenNoHistory()
    {
        AppDbContext db = CreateDbContext();

        var sut = new SearchHistoryService(db);

        Guid userId = Guid.NewGuid();

        IReadOnlyList<SearchHistoryItemDto> result = await sut.GetHistoryAsync(userId, 2);

        Assert.Empty(result);
    }
}
