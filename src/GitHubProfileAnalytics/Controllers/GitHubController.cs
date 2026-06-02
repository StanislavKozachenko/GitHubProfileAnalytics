using System.Security.Claims;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.Analytics;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController(
    IProfileCacheService profileCacheService,
    ISearchHistoryService searchHistoryService,
    IComparisonService comparisonService
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
            ? Unauthorized()
            : Ok(await searchHistoryService.GetHistoryAsync(userId, limit));
    }

    [HttpGet("compare")]
    public async Task<ActionResult<ProfileComparisonDto>> Compare(
        [FromQuery] string users
    )
    {
        string[] usernames =
            users?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return usernames.Length != 2
            ? (ActionResult<ProfileComparisonDto>)
                BadRequest("Exactly 2 usernames are required.")
            : (ActionResult<ProfileComparisonDto>)
                Ok(await comparisonService.CompareAsync(usernames[0], usernames[1]));
    }
}
