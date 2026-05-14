namespace GitHubProfileAnalytics.Domain;

public class AnalyticsCache(
    Guid id,
    string gitHubUserName,
    string data,
    DateTimeOffset cachedAt
)
{
    public Guid Id { get; init; } = id;
    public string GitHubUserName { get; init; } = gitHubUserName;
    public string Data { get; init; } = data;
    public DateTimeOffset CachedAt { get; init; } = cachedAt;
}
