using GitHubProfileAnalytics.DTOs.Analytics;

namespace GitHubProfileAnalytics.Services.Analytics;

public interface IComparisonService
{
    public Task<ProfileComparisonDto> CompareAsync(string username1, string username2);
}
