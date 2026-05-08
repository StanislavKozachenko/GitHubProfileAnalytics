using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GitHubProfileAnalytics.Tests;

public sealed class AuthServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                }
            )
            .Build();

    private static async Task<User> SeedUser(
        AppDbContext db,
        string email = "user@test.com",
        string password = "password"
    )
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task RegisterReturnsNullWhenEmailAlreadyExists()
    {
        var db = CreateDbContext();
        await SeedUser(db, email: "existing@test.com");
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.RegisterAsync(
            new RegisterRequest { Email = "existing@test.com", Password = "pass" }
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterCreatesUserInDatabase()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        await sut.RegisterAsync(new RegisterRequest { Email = "new@test.com", Password = "pass" });

        Assert.Single(db.Users);
        Assert.Equal("new@test.com", db.Users.Single().Email);
    }

    [Fact]
    public async Task RegisterHashesPasswordBeforeSaving()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        const string plainPassword = "mysecretpassword";

        await sut.RegisterAsync(
            new RegisterRequest { Email = "new@test.com", Password = plainPassword }
        );

        var storedHash = db.Users.Single().PasswordHash;
        Assert.NotEqual(plainPassword, storedHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(plainPassword, storedHash));
    }

    [Fact]
    public async Task RegisterReturnsAccessTokenAndRefreshToken()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.RegisterAsync(
            new RegisterRequest { Email = "new@test.com", Password = "pass" }
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task LoginReturnsNullWhenUserNotFound()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.LoginAsync(
            new LoginRequest { Email = "unknown@test.com", Password = "pass" }
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginReturnsNullWhenPasswordIsIncorrect()
    {
        var db = CreateDbContext();
        await SeedUser(db, email: "user@test.com", password: "correct");
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.LoginAsync(
            new LoginRequest { Email = "user@test.com", Password = "wrong" }
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginReturnsAccessTokenAndRefreshToken()
    {
        var db = CreateDbContext();
        await SeedUser(db, email: "user@test.com", password: "password");
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.LoginAsync(
            new LoginRequest { Email = "user@test.com", Password = "password" }
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task RefreshWithValidTokenReturnsNewTokenPair()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        var auth = await sut.RegisterAsync(
            new RegisterRequest { Email = "a@b.com", Password = "pass" }
        );

        var result = await sut.RefreshAsync(auth!.RefreshToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(auth.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task RefreshRevokesOldToken()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        var auth = await sut.RegisterAsync(
            new RegisterRequest { Email = "a@b.com", Password = "pass" }
        );
        var oldToken = auth!.RefreshToken;

        await sut.RefreshAsync(oldToken);

        var revoked = await db.RefreshTokens.FirstAsync(rt => rt.Token == oldToken);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task RefreshWithUnknownTokenReturnsNull()
    {
        var sut = new AuthService(CreateDbContext(), CreateConfiguration());

        var result = await sut.RefreshAsync("nonexistent-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshWithAlreadyUsedTokenReturnsNull()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        var auth = await sut.RegisterAsync(
            new RegisterRequest { Email = "a@b.com", Password = "pass" }
        );
        var oldToken = auth!.RefreshToken;

        await sut.RefreshAsync(oldToken);
        var result = await sut.RefreshAsync(oldToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshWithExpiredTokenReturnsNull()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        var auth = await sut.RegisterAsync(
            new RegisterRequest { Email = "a@b.com", Password = "pass" }
        );

        var token = await db.RefreshTokens.FirstAsync(rt => rt.Token == auth!.RefreshToken);
        token.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await sut.RefreshAsync(auth!.RefreshToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task AccessTokenContainsEmailClaim()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());
        const string email = "user@test.com";

        var result = await sut.RegisterAsync(
            new RegisterRequest { Email = email, Password = "pass" }
        );

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Email && c.Value == email);
    }

    [Fact]
    public async Task AccessTokenContainsUserIdClaim()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.RegisterAsync(
            new RegisterRequest { Email = "user@test.com", Password = "pass" }
        );

        var userId = db.Users.Single().Id.ToString();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
    }
}
