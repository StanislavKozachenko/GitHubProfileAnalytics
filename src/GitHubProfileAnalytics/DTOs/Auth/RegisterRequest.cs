namespace GitHubProfileAnalytics.DTOs.Auth;

public class RegisterRequest(string email, string password)
{
    public string Email { get; init; } = email;
    public string Password { get; init; } = password;
}
