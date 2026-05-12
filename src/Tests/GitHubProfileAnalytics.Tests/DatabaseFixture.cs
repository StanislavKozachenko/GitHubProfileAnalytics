using GitHubProfileAnalytics.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace GitHubProfileAnalytics.Tests;

public sealed class DatabaseFixture : IDisposable
{
    private readonly PostgreSqlContainer _postgres;
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        _postgres.StartAsync().GetAwaiter().GetResult();
        ConnectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        using var db = new AppDbContext(options);
        db.Database.Migrate();
    }

    public async Task TruncateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new AppDbContext(options);
        _ = await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "Users", "RefreshTokens", "ProfileCaches", "AnalyticsCaches", "SearchHistories" RESTART IDENTITY CASCADE;
            """
        );
    }

    public void Dispose()
    {
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
