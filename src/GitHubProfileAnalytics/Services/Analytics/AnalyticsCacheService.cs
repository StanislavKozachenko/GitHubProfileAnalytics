using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Services.Analytics;

public class AnalyticsCacheService : IAnalyticsCacheService
{
    private readonly AppDbContext _context;
    private readonly IAnalyticsService _analyticsService;
    private readonly IConfiguration _configuration;

    public AnalyticsCacheService(
        AppDbContext context,
        IAnalyticsService analyticsService,
        IConfiguration configuration
    )
    {
        _context = context;
        _analyticsService = analyticsService;
        _configuration = configuration;
    }

    public async Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username)
    {
        var threshold = CacheHelper.GetThreshold(_configuration, "AnalyticsCache:TtlHours");

        var cached = await _context.AnalyticsCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username && p.CachedAt >= threshold
        );

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<GitHubAnalyticsDto>(cached.Data)
                ?? throw new InvalidOperationException(
                    $"Corrupted cache entry for '{cached.GitHubUserName}'"
                );
        }

        var profileAnalytics = await _analyticsService.GetAnalyticsAsync(username);

        var entry = await _context.AnalyticsCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username
        );

        if (entry is null)
        {
            _context.AnalyticsCaches.Add(
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

        await _context.SaveChangesAsync();
        return profileAnalytics;
    }
}
