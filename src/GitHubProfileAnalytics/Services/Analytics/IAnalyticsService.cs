using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public interface IAnalyticsService
{
    Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username);
}
