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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public class GitHubControllerTests
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

                ServiceDescriptor? cacheDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IProfileCacheService)
                );
                _ = services.Remove(cacheDescriptor!);
                IProfileCacheService mockCache = Substitute.For<IProfileCacheService>();
                _ = mockCache
                    .GetProfileAsync(Arg.Any<string>())
                    .Returns(new GitHubProfileDto());
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
    public async Task GetProfileWithoutTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/github/someuser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfileWithTokenReturns200()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync("/api/github/someuser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.SearchHistories.AnyAsync());
    }

    [Fact]
    public async Task GetProfileWithUndefinedUserReturns404()
    {
        IProfileCacheService profileCacheService = Substitute.For<IProfileCacheService>();

        HttpClient client = _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    ServiceDescriptor? cacheDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IProfileCacheService)
                    );
                    _ = services.Remove(cacheDescriptor!);

                    _ = profileCacheService
                        .GetProfileAsync(Arg.Any<string>())
                        .Throws(
                            new NotFoundException("Not found", HttpStatusCode.NotFound)
                        );
                    _ = services.AddSingleton(profileCacheService);
                })
            )
            .CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync("/api/github/unknownuser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
