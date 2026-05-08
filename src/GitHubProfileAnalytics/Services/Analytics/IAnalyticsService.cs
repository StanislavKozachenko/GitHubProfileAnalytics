using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public interface IAnalyticsService
{
    public Task<GitHubAnalyticsDto> GetAnalyticsAsync(string username);
}
