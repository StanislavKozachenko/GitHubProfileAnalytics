using System.Security.Claims;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController(
    IProfileCacheService profileCacheService,
    ISearchHistoryService searchHistoryService
) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<GitHubProfileDto>> GetProfile(string username)
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized();
        }

        GitHubProfileDto profile = await profileCacheService.GetProfileAsync(username);

        await searchHistoryService.AddAsync(userId, username);

        return profile;
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<SearchHistoryItemDto>>> SearchHistory(
        int limit = 100
    )
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim is null || !Guid.TryParse(userIdClaim, out Guid userId)
            ? (ActionResult<IReadOnlyList<SearchHistoryItemDto>>)Unauthorized()
            : (ActionResult<IReadOnlyList<SearchHistoryItemDto>>)
                Ok(await searchHistoryService.GetHistoryAsync(userId, limit));
    }
}
