using System.Text.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Services;

public class ProfileCacheService : IProfileCacheService
{
    private readonly AppDbContext _context;
    private readonly IGitHubService _gitHubService;
    private readonly IConfiguration _configuration;

    public ProfileCacheService(
        AppDbContext context,
        IGitHubService gitHubService,
        IConfiguration configuration
    )
    {
        _context = context;
        _gitHubService = gitHubService;
        _configuration = configuration;
    }

    public async Task<GitHubProfileDto> GetProfileAsync(string username)
    {
        var ttlHours = _configuration.GetValue<int>("ProfileCache:TtlHours", 1);
        var threshold = DateTimeOffset.UtcNow.AddHours(-ttlHours);

        var cached = await _context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username && p.CachedAt >= threshold
        );

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<GitHubProfileDto>(cached.Data)
                ?? throw new InvalidOperationException(
                    $"Corrupted cache entry for '{cached.GitHubUserName}'"
                );
        }

        var profile = await _gitHubService.GetProfileAsync(username);

        var entry = await _context.ProfileCaches.FirstOrDefaultAsync(p =>
            p.GitHubUserName == username
        );

        if (entry is null)
        {
            _context.ProfileCaches.Add(
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

        await _context.SaveChangesAsync();
        return profile;
    }
}
