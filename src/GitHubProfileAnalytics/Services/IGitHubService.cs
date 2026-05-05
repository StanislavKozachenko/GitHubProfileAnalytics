using GitHubProfileAnalytics.DTOs;

namespace GitHubProfileAnalytics.Services;

public interface IGitHubService
{
    Task<GitHubProfileDto> GetProfileAsync(string username);
}
