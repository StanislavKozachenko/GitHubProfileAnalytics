using System.Net;
using System.Net.Http.Json;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.DTOs.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubProfileAnalytics.Tests;

public sealed class AuthControllerTests
{
    private const string JwtKey =
        "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256";
    private const string JwtIssuer = "test-issuer";
    private const string JwtAudience = "test-audience";

    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            _ = builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Jwt:Key"] = JwtKey,
                            ["Jwt:Issuer"] = JwtIssuer,
                            ["Jwt:Audience"] = JwtAudience,
                            ["GitHub:Token"] = "test-token",
                        }
                    )
            );

            _ = builder.ConfigureServices(services =>
            {
                ServiceDescriptor? dbContextDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(AppDbContext)
                );
                _ = services.Remove(dbContextDescriptor!);

                ServiceDescriptor? dbOptionsDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                );
                _ = services.Remove(dbOptionsDescriptor!);

                string dbName = Guid.NewGuid().ToString();
                _ = services.AddScoped(_ => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(dbName)
                        .Options
                ));
            });
        });

    [Fact]
    public async Task RegisterReturns200WithAccessToken()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse? content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(content);
        Assert.NotEmpty(content.AccessToken);
        Assert.NotEmpty(content.RefreshToken);
    }

    [Fact]
    public async Task RegisterWithExistingEmailReturns409()
    {
        HttpClient client = _factory.CreateClient();

        _ = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LoginReturns200WithAccessToken()
    {
        HttpClient client = _factory.CreateClient();

        _ = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse? content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(content);
        Assert.NotEmpty(content.AccessToken);
        Assert.NotEmpty(content.RefreshToken);
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturns401()
    {
        HttpClient client = _factory.CreateClient();

        _ = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(
                new { Email = "test@test.com", Password = "wrongpassword" }
            )
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithUnknownEmailReturns401()
    {
        HttpClient client = _factory.CreateClient();

        _ = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(new { Email = "wrong@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshWithValidTokenReturns200WithNewTokenPair()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage registerResponse = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );
        AuthResponse? auth =
            await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/refresh",
            JsonContent.Create(new { auth!.RefreshToken })
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse? result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(auth.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task RefreshWithUnknownTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/refresh",
            JsonContent.Create(new { RefreshToken = "nonexistent-token" })
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshWithAlreadyUsedTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage registerResponse = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );
        AuthResponse? auth =
            await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _ = await client.PostAsync(
            "/api/auth/refresh",
            JsonContent.Create(new { auth!.RefreshToken })
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/auth/refresh",
            JsonContent.Create(new { auth.RefreshToken })
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
