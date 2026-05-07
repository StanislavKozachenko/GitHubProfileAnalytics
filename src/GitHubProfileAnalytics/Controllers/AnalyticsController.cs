using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController(IAnalyticsCacheService cacheService) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<GitHubAnalyticsDto>> GetAnalytics(string username)
    {
        var analytics = await cacheService.GetAnalyticsAsync(username);
        return analytics;
    }
}
