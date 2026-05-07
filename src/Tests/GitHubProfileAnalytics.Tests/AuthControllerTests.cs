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
    private const string JwtKey = "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256";
    private const string JwtIssuer = "test-issuer";
    private const string JwtAudience = "test-audience";

    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, config) =>
                {
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Jwt:Key"] = JwtKey,
                            ["Jwt:Issuer"] = JwtIssuer,
                            ["Jwt:Audience"] = JwtAudience,
                            ["GitHub:Token"] = "test-token",
                        }
                    );
                }
            );

            builder.ConfigureServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(AppDbContext)
                );
                services.Remove(dbContextDescriptor!);

                var dbOptionsDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                );
                services.Remove(dbOptionsDescriptor!);

                var dbName = Guid.NewGuid().ToString();
                services.AddScoped(_ => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options
                ));
            });
        });

    [Fact]
    public async Task RegisterReturns200WithAccessToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(content?.AccessToken);
        Assert.NotEmpty(content.AccessToken);
    }

    [Fact]
    public async Task RegisterWithExistingEmailReturns409()
    {
        var client = _factory.CreateClient();

        await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        var response = await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LoginReturns200WithAccessToken()
    {
        var client = _factory.CreateClient();

        await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        var response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(content?.AccessToken);
        Assert.NotEmpty(content.AccessToken);
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturns401()
    {
        var client = _factory.CreateClient();

        await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        var response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(new { Email = "test@test.com", Password = "wrongpassword" })
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithUnknownEmailReturns401()
    {
        var client = _factory.CreateClient();

        await client.PostAsync(
            "/api/auth/register",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        var response = await client.PostAsync(
            "/api/auth/login",
            JsonContent.Create(new { Email = "wrong@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshReturns501()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/auth/refresh",
            JsonContent.Create(new { Email = "test@test.com", Password = "password" })
        );

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
