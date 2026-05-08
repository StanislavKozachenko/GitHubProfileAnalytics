using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.DTOs.GitHub;

namespace GitHubProfileAnalytics.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> RefreshAsync(string token);
}
