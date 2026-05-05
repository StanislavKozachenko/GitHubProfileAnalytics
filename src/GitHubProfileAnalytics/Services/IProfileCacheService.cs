using GitHubProfileAnalytics.DTOs;

namespace GitHubProfileAnalytics.Services;

public interface IProfileCacheService
{
    Task<GitHubProfileDto> GetProfileAsync(string username);
}
