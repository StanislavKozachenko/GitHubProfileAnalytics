using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.GitHub;
using Microsoft.EntityFrameworkCore;

namespace GitHubProfileAnalytics.Services.GitHub;

public class SearchHistoryService(AppDbContext context) : ISearchHistoryService
{
    public async Task AddAsync(Guid userId, string gitHubUserName)
    {
        _ = context.SearchHistories.Add(
            new SearchHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GitHubUserName = gitHubUserName,
                SearchedAt = DateTimeOffset.UtcNow,
            }
        );

        _ = await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SearchHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        int limit
    )
    {
        return await context
            .SearchHistories.Where(h => h.UserId == userId)
            .OrderByDescending(h => h.SearchedAt)
            .Take(limit)
            .Select(h => new SearchHistoryItemDto
            {
                GitHubUserName = h.GitHubUserName,
                SearchedAt = h.SearchedAt,
            })
            .ToListAsync();
    }
}
