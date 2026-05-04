using GitHubProfileAnalytics.Domain;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<ProfileCache> ProfileCaches { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }
}
