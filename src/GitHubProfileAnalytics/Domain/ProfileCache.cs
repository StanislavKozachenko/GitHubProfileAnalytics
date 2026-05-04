namespace GitHubProfileAnalytics.Domain;

public class ProfileCache
{
    public Guid Id { get; set; }
    public string GitHubUserName { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTimeOffset CachedAt { get; set; }
}
