using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.GitHub;

public interface IGitHubService
{
    public Task<GitHubProfileDto> GetProfileAsync(string username);
}
