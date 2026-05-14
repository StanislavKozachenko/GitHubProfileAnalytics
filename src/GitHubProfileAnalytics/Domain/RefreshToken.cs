namespace GitHubProfileAnalytics.Domain;

public class RefreshToken(
    Guid id,
    string token,
    Guid userId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt
)
{
    public Guid Id { get; init; } = id;
    public string Token { get; init; } = token;
    public Guid UserId { get; init; } = userId;
    public DateTimeOffset CreatedAt { get; init; } = createdAt;
    public DateTimeOffset ExpiresAt { get; init; } = expiresAt;
    public DateTimeOffset? RevokedAt { get; private set; }

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
    }
}
