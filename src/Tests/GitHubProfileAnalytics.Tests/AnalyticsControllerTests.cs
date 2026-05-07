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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public sealed class AnalyticsControllerTests
{
    private const string JwtKey = "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256";
    private const string JwtIssuer = "test-issuer";
    private const string JwtAudience = "test-audience";

    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                );
                services.Remove(dbDescriptor!);
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                );

                var cacheDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IAnalyticsCacheService)
                );
                services.Remove(cacheDescriptor!);
                var mockCache = Substitute.For<IAnalyticsCacheService>();
                mockCache.GetAnalyticsAsync(Arg.Any<string>()).Returns(new GitHubAnalyticsDto());
                services.AddSingleton(mockCache);

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
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
                        };
                    }
                );
            });
        });

    private static string GenerateToken() =>
        new JwtSecurityTokenHandler().WriteToken(
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

    [Fact]
    public async Task GetAnalyticsWithoutTokenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/analytics/someuser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsWithTokenReturns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        var response = await client.GetAsync("/api/analytics/someuser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsWithUndefinedUserReturns404()
    {
        var analyticsCacheService = Substitute.For<IAnalyticsCacheService>();

        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var cacheDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IAnalyticsCacheService)
                    );
                    services.Remove(cacheDescriptor!);

                    analyticsCacheService
                        .GetAnalyticsAsync(Arg.Any<string>())
                        .Throws(
                            new NotFoundException("Not found", System.Net.HttpStatusCode.NotFound)
                        );
                    services.AddSingleton(analyticsCacheService);
                });
            })
            .CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        var response = await client.GetAsync("/api/analytics/unknownuser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
