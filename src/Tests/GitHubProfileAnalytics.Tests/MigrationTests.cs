using GitHubProfileAnalytics.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace GitHubProfileAnalytics.Tests;

public sealed class MigrationTests
{
    [Fact]
    public async Task MigrateOnFreshDatabaseSucceeds()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync();

        await using AppDbContext db = new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options
        );

        Exception? exception = await Record.ExceptionAsync(() =>
            db.Database.MigrateAsync()
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task MigrateIsIdempotent()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync();

        await using AppDbContext db = new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options
        );

        await db.Database.MigrateAsync();
        Exception? exception = await Record.ExceptionAsync(() =>
            db.Database.MigrateAsync()
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task NoPendingMigrationsAfterMigrate()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync();

        await using AppDbContext db = new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options
        );

        await db.Database.MigrateAsync();

        IEnumerable<string> pending = await db.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }
}
