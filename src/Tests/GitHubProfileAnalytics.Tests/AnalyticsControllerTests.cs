using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.Services.Analytics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public sealed class AnalyticsControllerTests
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
                ServiceDescriptor? dbDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                );
                _ = services.Remove(dbDescriptor!);
                _ = services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                );

                ServiceDescriptor? cacheDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IAnalyticsCacheService)
                );
                _ = services.Remove(cacheDescriptor!);
                IAnalyticsCacheService mockCache =
                    Substitute.For<IAnalyticsCacheService>();
                _ = mockCache
                    .GetAnalyticsAsync(Arg.Any<string>())
                    .Returns(new GitHubAnalyticsDto());
                _ = services.AddSingleton(mockCache);

                _ = services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = JwtIssuer,
                            ValidAudience = JwtAudience,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(JwtKey)
                            ),
                        }
                );
            });
        });

    private static string GenerateToken()
    {
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: JwtIssuer,
                audience: JwtAudience,
                claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                    SecurityAlgorithms.HmacSha256
                )
            )
        );
    }

    [Fact]
    public async Task GetAnalyticsWithoutTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/analytics/someuser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsWithTokenReturns200()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync("/api/analytics/someuser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsWithUndefinedUserReturns404()
    {
        IAnalyticsCacheService analyticsCacheService =
            Substitute.For<IAnalyticsCacheService>();

        HttpClient client = _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    ServiceDescriptor? cacheDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IAnalyticsCacheService)
                    );
                    _ = services.Remove(cacheDescriptor!);

                    _ = analyticsCacheService
                        .GetAnalyticsAsync(Arg.Any<string>())
                        .Throws(
                            new NotFoundException("Not found", HttpStatusCode.NotFound)
                        );
                    _ = services.AddSingleton(analyticsCacheService);
                })
            )
            .CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync(
            "/api/analytics/unknownuser"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
