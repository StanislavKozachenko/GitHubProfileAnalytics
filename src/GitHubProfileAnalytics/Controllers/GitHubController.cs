using System.Security.Claims;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.Analytics;
using GitHubProfileAnalytics.Services.Auth;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octokit;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController : ControllerBase
{
    private readonly IProfileCacheService _profileCacheService;
    private readonly AppDbContext _context;

    public GitHubController(IProfileCacheService profileCacheService, AppDbContext context)
    {
        _profileCacheService = profileCacheService;
        _context = context;
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<GitHubProfileDto>> GetProfile(string username)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _profileCacheService.GetProfileAsync(username);

            _context.SearchHistories.Add(
                new SearchHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    GitHubUserName = username,
                    SearchedAt = DateTimeOffset.UtcNow,
                }
            );

            await _context.SaveChangesAsync();

            return profile;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
