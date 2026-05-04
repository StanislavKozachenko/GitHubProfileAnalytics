namespace GitHubProfileAnalytics.Domain;

public class SearchHistory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string GitHubUserName { get; set; } = string.Empty;
    public DateTimeOffset SearchedAt { get; set; }
}
