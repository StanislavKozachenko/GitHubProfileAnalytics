using GitHubProfileAnalytics.DTOs;

namespace GitHubProfileAnalytics.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
}
