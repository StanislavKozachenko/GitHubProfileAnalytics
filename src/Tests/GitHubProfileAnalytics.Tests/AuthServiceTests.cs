using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GitHubProfileAnalytics.Tests;

[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class AuthServiceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.TruncateAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    private AppDbContext CreateDbContext()
    {
        return new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options
        );
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] =
                        "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                }
            )
            .Build();
    }

    private static async Task<User> SeedUser(
        AppDbContext db,
        string email = "user@test.com",
        string password = "password"
    )
    {
        var user = new User(
            Guid.NewGuid(),
            email,
            BCrypt.Net.BCrypt.HashPassword(password),
            DateTimeOffset.UtcNow
        );
        _ = db.Users.Add(user);
        _ = await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task RegisterReturnsNullWhenEmailAlreadyExists()
    {
        AppDbContext db = CreateDbContext();
        _ = await SeedUser(db, email: "existing@test.com");
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.RegisterAsync(
            new RegisterRequest("existing@test.com", "pass")
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterCreatesUserInDatabase()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        _ = await sut.RegisterAsync(new RegisterRequest("new@test.com", "pass"));

        _ = Assert.Single(db.Users);
        Assert.Equal("new@test.com", db.Users.Single().Email);
    }

    [Fact]
    public async Task RegisterHashesPasswordBeforeSaving()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        const string plainPassword = "mysecretpassword";

        _ = await sut.RegisterAsync(new RegisterRequest("new@test.com", plainPassword));

        string storedHash = db.Users.Single().PasswordHash;
        Assert.NotEqual(plainPassword, storedHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(plainPassword, storedHash));
    }

    [Fact]
    public async Task RegisterReturnsAccessTokenAndRefreshToken()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.RegisterAsync(
            new RegisterRequest("new@test.com", "pass")
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task LoginReturnsNullWhenUserNotFound()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.LoginAsync(
            new LoginRequest("unknown@test.com", "pass")
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginReturnsNullWhenPasswordIsIncorrect()
    {
        AppDbContext db = CreateDbContext();
        _ = await SeedUser(db, email: "user@test.com", password: "correct");
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.LoginAsync(
            new LoginRequest("user@test.com", "wrong")
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginReturnsAccessTokenAndRefreshToken()
    {
        AppDbContext db = CreateDbContext();
        _ = await SeedUser(db, email: "user@test.com", password: "password");
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.LoginAsync(
            new LoginRequest("user@test.com", "password")
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task RefreshWithValidTokenReturnsNewTokenPair()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        AuthResponse? auth = await sut.RegisterAsync(
            new RegisterRequest("a@b.com", "pass")
        );

        AuthResponse? result = await sut.RefreshAsync(auth!.RefreshToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(auth.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task RefreshRevokesOldToken()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        AuthResponse? auth = await sut.RegisterAsync(
            new RegisterRequest("a@b.com", "pass")
        );
        string oldToken = auth!.RefreshToken;

        _ = await sut.RefreshAsync(oldToken);

        RefreshToken revoked = await db.RefreshTokens.FirstAsync(rt =>
            rt.Token == oldToken
        );
        _ = Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task RefreshWithUnknownTokenReturnsNull()
    {
        var sut = new AuthService(CreateDbContext(), CreateConfiguration());

        AuthResponse? result = await sut.RefreshAsync("nonexistent-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshWithAlreadyUsedTokenReturnsNull()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        AuthResponse? auth = await sut.RegisterAsync(
            new RegisterRequest("a@b.com", "pass")
        );
        string oldToken = auth!.RefreshToken;

        _ = await sut.RefreshAsync(oldToken);
        AuthResponse? result = await sut.RefreshAsync(oldToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshWithExpiredTokenReturnsNull()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        AuthResponse? auth = await sut.RegisterAsync(
            new RegisterRequest("a@b.com", "pass")
        );

        string tokenValue = auth!.RefreshToken;
        RefreshToken original = await db.RefreshTokens.FirstAsync(rt =>
            rt.Token == tokenValue
        );
        _ = db.RefreshTokens.Remove(original);
        _ = db.RefreshTokens.Add(
            new RefreshToken(
                original.Id,
                original.Token,
                original.UserId,
                original.CreatedAt,
                DateTimeOffset.UtcNow.AddDays(-1)
            )
        );
        _ = await db.SaveChangesAsync();

        AuthResponse? result = await sut.RefreshAsync(tokenValue);

        Assert.Null(result);
    }

    [Fact]
    public async Task AccessTokenContainsEmailClaim()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        const string email = "user@test.com";

        AuthResponse? result = await sut.RegisterAsync(
            new RegisterRequest(email, "pass")
        );

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            result!.AccessToken
        );
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Email && c.Value == email);
    }

    [Fact]
    public async Task AccessTokenContainsUserIdClaim()
    {
        AppDbContext db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        AuthResponse? result = await sut.RegisterAsync(
            new RegisterRequest("user@test.com", "pass")
        );

        string userId = db.Users.Single().Id.ToString();
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            result!.AccessToken
        );
        Assert.Contains(
            jwt.Claims,
            c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId
        );
    }
}
