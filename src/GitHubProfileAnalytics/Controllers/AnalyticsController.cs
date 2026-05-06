using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsCacheService _cacheService;

    public AnalyticsController(IAnalyticsCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<GitHubAnalyticsDto>> GetAnalytics(string username)
    {
        var analytics = await _cacheService.GetAnalyticsAsync(username);
        return analytics;
    }
}
