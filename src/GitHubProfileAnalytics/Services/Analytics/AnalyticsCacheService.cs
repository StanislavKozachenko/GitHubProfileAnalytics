using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Services.Analytics;

public class AnalyticsCacheService(
    AppDbContext context,
    IAnalyticsService analyticsService,
    IConfiguration configuration
) : IAnalyticsCacheService
{
    public async Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username)
    {
        var threshold = CacheHelper.GetThreshold(configuration, "AnalyticsCache:TtlHours");

        var cached = await context.AnalyticsCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username && p.CachedAt >= threshold
        );

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<GitHubAnalyticsDto>(cached.Data)
                ?? throw new InvalidOperationException(
                    $"Corrupted cache entry for '{cached.GitHubUserName}'"
                );
        }

        var profileAnalytics = await analyticsService.GetAnalyticsAsync(username);

        var entry = await context.AnalyticsCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username
        );

        if (entry is null)
        {
            context.AnalyticsCaches.Add(
                new AnalyticsCache
                {
                    Id = Guid.NewGuid(),
                    GitHubUserName = username,
                    Data = JsonSerializer.Serialize(profileAnalytics),
                    CachedAt = DateTimeOffset.UtcNow,
                }
            );
        }
        else
        {
            entry.Data = JsonSerializer.Serialize(profileAnalytics);
            entry.CachedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
        return profileAnalytics;
    }
}
