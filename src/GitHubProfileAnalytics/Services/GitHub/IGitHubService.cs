using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.GitHub;

public interface IGitHubService
{
    Task<GitHubProfileDto> GetProfileAsync(string username);
}
