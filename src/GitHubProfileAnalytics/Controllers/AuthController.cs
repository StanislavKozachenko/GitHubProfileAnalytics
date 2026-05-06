using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.Analytics;
using GitHubProfileAnalytics.Services.Auth;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (result is null)
        {
            return Conflict();
        }

        return result;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result is null)
        {
            return Unauthorized();
        }

        return result;
    }

    [HttpPost("refresh")]
    public ActionResult<AuthResponse> Refresh()
    {
        return StatusCode(501);
    }
}
