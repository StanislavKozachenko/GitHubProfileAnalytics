using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.GitHub;

public interface ISearchHistoryService
{
    public Task AddAsync(Guid userId, string gitHubUserName);
    public Task<IReadOnlyList<SearchHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        int limit
    );
}
