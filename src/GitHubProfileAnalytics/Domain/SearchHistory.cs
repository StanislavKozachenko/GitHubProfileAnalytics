namespace GitHubProfileAnalytics.Domain;

public class SearchHistory(
    Guid id,
    Guid userId,
    string gitHubUserName,
    DateTimeOffset searchedAt
)
{
    public Guid Id { get; init; } = id;
    public Guid UserId { get; init; } = userId;
    public string GitHubUserName { get; init; } = gitHubUserName;
    public DateTimeOffset SearchedAt { get; init; } = searchedAt;
}
