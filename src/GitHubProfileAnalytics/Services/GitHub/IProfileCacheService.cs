using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.GitHub;

public interface IProfileCacheService
{
    public Task<GitHubProfileDto> GetProfileAsync(string username);
}
