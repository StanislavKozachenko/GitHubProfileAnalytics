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
    public async Task RegisterReturnsAccessToken()
    {
        var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.RegisterAsync(
            new RegisterRequest { Email = "new@test.com", Password = "pass" }
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
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
    public async Task LoginReturnsAccessToken()
    {
        var db = CreateDbContext();
        await SeedUser(db, email: "user@test.com", password: "password");
        var sut = new AuthService(db, CreateConfiguration());

        var result = await sut.LoginAsync(
            new LoginRequest { Email = "user@test.com", Password = "password" }
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
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
