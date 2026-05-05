using GitHubProfileAnalytics.DTOs;
using Octokit;

namespace GitHubProfileAnalytics.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;

    public GitHubService(GitHubClient client)
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
