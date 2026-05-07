using System.Security.Claims;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController(IProfileCacheService profileCacheService, AppDbContext context)
    : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<GitHubProfileDto>> GetProfile(string username)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var profile = await profileCacheService.GetProfileAsync(username);

        context.SearchHistories.Add(
            new SearchHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GitHubUserName = username,
                SearchedAt = DateTimeOffset.UtcNow,
            }
        );

        await context.SaveChangesAsync();

        return profile;
    }
}
