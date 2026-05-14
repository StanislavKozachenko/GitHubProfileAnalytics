namespace GitHubProfileAnalytics.DTOs.GitHub;

public class SearchHistoryItemDto(string gitHubUserName, DateTimeOffset searchedAt)
{
    public string GitHubUserName { get; init; } = gitHubUserName;
    public DateTimeOffset SearchedAt { get; init; } = searchedAt;
}
