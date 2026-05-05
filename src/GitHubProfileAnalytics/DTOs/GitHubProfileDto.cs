namespace GitHubProfileAnalytics.DTOs;

public class GitHubProfileDto
{
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
