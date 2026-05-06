using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;
using Octokit;

namespace GitHubProfileAnalytics.Services.GitHub;

public class GitHubService : IGitHubService
{
    private readonly IGitHubClient _client;

    public GitHubService(IGitHubClient client)
    {
        _client = client;
    }

    public async Task<GitHubProfileDto> GetProfileAsync(string username)
    {
        var user = await _client.User.Get(username);

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
