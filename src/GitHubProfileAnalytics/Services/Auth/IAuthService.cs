using GitHubProfileAnalytics.DTOs.Auth;

namespace GitHubProfileAnalytics.Services.Auth;

public interface IAuthService
{
    public Task<AuthResponse?> LoginAsync(LoginRequest request);
    public Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    public Task<AuthResponse?> RefreshAsync(string token);
}
