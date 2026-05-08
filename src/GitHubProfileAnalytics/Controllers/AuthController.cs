using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        AuthResponse? result = await authService.RegisterAsync(request);
        return result is null
            ? (ActionResult<AuthResponse>)Conflict()
            : (ActionResult<AuthResponse>)result;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        AuthResponse? result = await authService.LoginAsync(request);
        return result is null
            ? (ActionResult<AuthResponse>)Unauthorized()
            : (ActionResult<AuthResponse>)result;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        AuthResponse? result = await authService.RefreshAsync(request.RefreshToken);
        return result is null
            ? (ActionResult<AuthResponse>)Unauthorized()
            : (ActionResult<AuthResponse>)result;
    }
}
