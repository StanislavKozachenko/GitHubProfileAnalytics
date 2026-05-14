namespace GitHubProfileAnalytics.DTOs.GitHub;

public class GitHubProfileDto(
    string login,
    string name,
    string avatarUrl,
    string bio,
    int publicRepos,
    int followers,
    int following,
    DateTimeOffset createdAt
)
{
    public string Login { get; init; } = login;
    public string Name { get; init; } = name;
    public string AvatarUrl { get; init; } = avatarUrl;
    public string Bio { get; init; } = bio;
    public int PublicRepos { get; init; } = publicRepos;
    public int Followers { get; init; } = followers;
    public int Following { get; init; } = following;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}
