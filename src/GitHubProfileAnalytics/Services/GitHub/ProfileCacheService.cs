using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Services.GitHub;

public class ProfileCacheService(
    AppDbContext context,
    IGitHubService gitHubService,
    IConfiguration configuration
) : IProfileCacheService
{
    public async Task<GitHubProfileDto> GetProfileAsync(string username)
    {
        var threshold = CacheHelper.GetThreshold(configuration, "ProfileCache:TtlHours");

        var cached = await context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username && p.CachedAt >= threshold
        );

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<GitHubProfileDto>(cached.Data)
                ?? throw new InvalidOperationException(
                    $"Corrupted cache entry for '{cached.GitHubUserName}'"
                );
        }

        var profile = await gitHubService.GetProfileAsync(username);

        var entry = await context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username
        );

        if (entry is null)
        {
            context.ProfileCaches.Add(
                new ProfileCache
                {
                    Id = Guid.NewGuid(),
                    GitHubUserName = username,
                    Data = JsonSerializer.Serialize(profile),
                    CachedAt = DateTimeOffset.UtcNow,
                }
            );
        }
        else
        {
            entry.Data = JsonSerializer.Serialize(profile);
            entry.CachedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
        return profile;
    }
}
