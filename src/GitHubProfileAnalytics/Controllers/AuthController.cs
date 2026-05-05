using GitHubProfileAnalytics.DTOs;
using GitHubProfileAnalytics.Services;
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
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (result is null)
        {
            return Conflict();
        }

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result is null)
        {
            return Unauthorized();
        }
        return Ok(result);
    }

    [HttpPost("refresh")]
    public IActionResult Refresh()
    {
        return StatusCode(501);
    }
}
