namespace GitHubProfileAnalytics.DTOs.Auth;

public class RefreshRequest(string refreshToken)
{
    public string RefreshToken { get; init; } = refreshToken;
}
