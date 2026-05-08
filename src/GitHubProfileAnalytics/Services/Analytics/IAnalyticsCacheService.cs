using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public interface IAnalyticsCacheService
{
    public Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username);
}
