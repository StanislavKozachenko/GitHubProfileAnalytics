using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
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
        DateTimeOffset threshold = CacheHelper.GetThreshold(
            configuration,
            "ProfileCache:TtlHours"
        );

        ProfileCache? cached = await context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username && p.CachedAt >= threshold
        );

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<GitHubProfileDto>(cached.Data)
                ?? throw new InvalidOperationException(
                    $"Corrupted cache entry for '{cached.GitHubUserName}'"
                );
        }

        GitHubProfileDto profile = await gitHubService.GetProfileAsync(username);

        ProfileCache? entry = await context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username
        );

        if (entry is null)
        {
            _ = context.ProfileCaches.Add(
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

        _ = await context.SaveChangesAsync();
        return profile;
    }
}
