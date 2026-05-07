using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;
using Octokit;

namespace GitHubProfileAnalytics.Services.GitHub;

public class GitHubService(IGitHubClient client) : IGitHubService
{
    public async Task<GitHubProfileDto> GetProfileAsync(string username)
    {
        var user = await client.User.Get(username);

        return new GitHubProfileDto
        {
            Login = user.Login,
            Name = user.Name,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio,
            PublicRepos = user.PublicRepos,
            Followers = user.Followers,
            Following = user.Following,
            CreatedAt = user.CreatedAt,
        };
    }
}
