namespace GitHubProfileAnalytics.Domain;

public class User(Guid id, string email, string passwordHash, DateTimeOffset createdAt)
{
    public Guid Id { get; init; } = id;
    public string Email { get; init; } = email;
    public string PasswordHash { get; init; } = passwordHash;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
