using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public class GitHubControllerTests
{
    private const string JwtKey = "test-jwt-secret-key-that-is-long-enough-for-hmac-sha256";
    private const string JwtIssuer = "test-issuer";
    private const string JwtAudience = "test-audience";

    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
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

                var cacheDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IProfileCacheService)
                );
                services.Remove(cacheDescriptor!);
                var mockCache = Substitute.For<IProfileCacheService>();
                mockCache.GetProfileAsync(Arg.Any<string>()).Returns(new GitHubProfileDto());
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
    public async Task GetProfileWithoutTokenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/github/someuser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfileWithTokenReturns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        var response = await client.GetAsync("/api/github/someuser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.SearchHistories.AnyAsync());
    }

    [Fact]
    public async Task GetProfileWithUndefinedUserReturns404()
    {
        var profileCacheService = Substitute.For<IProfileCacheService>();

        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var cacheDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IProfileCacheService)
                    );
                    services.Remove(cacheDescriptor!);

                    profileCacheService
                        .GetProfileAsync(Arg.Any<string>())
                        .Throws(
                            new NotFoundException("Not found", System.Net.HttpStatusCode.NotFound)
                        );
                    services.AddSingleton(profileCacheService);
                });
            })
            .CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        var response = await client.GetAsync("/api/github/unknownuser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
