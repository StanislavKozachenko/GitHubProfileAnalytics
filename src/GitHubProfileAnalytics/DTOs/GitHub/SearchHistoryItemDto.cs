namespace GitHubProfileAnalytics.DTOs.GitHub;

public class SearchHistoryItemDto
{
    public string GitHubUserName { get; set; } = string.Empty;
    public DateTimeOffset SearchedAt { get; set; }
}
