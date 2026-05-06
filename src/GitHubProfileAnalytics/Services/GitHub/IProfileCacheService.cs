using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.GitHub;

public interface IProfileCacheService
{
    Task<GitHubProfileDto> GetProfileAsync(string username);
}
