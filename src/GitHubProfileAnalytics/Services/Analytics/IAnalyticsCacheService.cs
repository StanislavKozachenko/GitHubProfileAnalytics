using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public interface IAnalyticsCacheService
{
    Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username);
}
