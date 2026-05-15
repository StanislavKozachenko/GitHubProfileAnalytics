using GitHubProfileAnalytics.DTOs.GitHub;
using Octokit;

namespace GitHubProfileAnalytics.Services.GitHub;

public class GitHubService(IGitHubClient client) : IGitHubService
{
    public async Task<GitHubProfileDto> GetProfileAsync(string username)
    {
        User user = await client.User.Get(username);

        return new GitHubProfileDto(
            user.Login,
            user.Name,
            user.AvatarUrl,
            user.Bio,
            user.PublicRepos,
            user.Followers,
            user.Following,
            user.CreatedAt
        );
    }
}
