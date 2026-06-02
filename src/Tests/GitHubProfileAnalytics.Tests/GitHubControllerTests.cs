using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.DTOs.Analytics;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.Analytics;
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

[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class GitHubControllerTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.TruncateAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

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

                _ = services.AddScoped(_ => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseNpgsql(fixture.ConnectionString)
                        .Options
                ));

                ServiceDescriptor? cacheDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IProfileCacheService)
                );
                _ = services.Remove(cacheDescriptor!);
                IProfileCacheService mockCache = Substitute.For<IProfileCacheService>();
                _ = mockCache
                    .GetProfileAsync(Arg.Any<string>())
                    .Returns(
                        new GitHubProfileDto(
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            0,
                            0,
                            0,
                            default
                        )
                    );
                _ = services.AddSingleton(mockCache);

                ServiceDescriptor? comparisonDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IComparisonService)
                );
                _ = services.Remove(comparisonDescriptor!);
                IComparisonService mockComparison = Substitute.For<IComparisonService>();
                GitHubAnalyticsDto emptyAnalytics = new(
                    new ProfileMetrics(0, 0, 0),
                    new RepositoryMetrics(0, 0, 0, []),
                    new ActivityMetrics(0, 0, 0, 0, 0),
                    []
                );
                _ = mockComparison
                    .CompareAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(
                        new ProfileComparisonDto([
                            new ComparisonEntryDto(
                                "user1",
                                50.0,
                                emptyAnalytics.Profile,
                                emptyAnalytics.Repositories,
                                emptyAnalytics.Activity,
                                emptyAnalytics.ContributionGraph
                            ),
                            new ComparisonEntryDto(
                                "user2",
                                50.0,
                                emptyAnalytics.Profile,
                                emptyAnalytics.Repositories,
                                emptyAnalytics.Activity,
                                emptyAnalytics.ContributionGraph
                            ),
                        ])
                    );
                _ = services.AddSingleton(mockComparison);

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

    [Fact]
    public async Task GetHistoryWithoutTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/github/history?limit=100"
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHistoryReturnsOnlyCurrentUserHistory()
    {
        HttpClient client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpClient client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        _ = await client1.GetAsync("/api/github/user1profile");
        _ = await client2.GetAsync("/api/github/user2profile");

        HttpResponseMessage response = await client1.GetAsync(
            "/api/github/history?limit=100"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<SearchHistoryItemDto>? history =
            await response.Content.ReadFromJsonAsync<
                IReadOnlyList<SearchHistoryItemDto>
            >();
        Assert.NotNull(history);
        _ = Assert.Single(history);
        Assert.Equal("user1profile", history[0].GitHubUserName);
    }

    [Fact]
    public async Task GetHistoryWithTokenReturns200()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync(
            "/api/github/history?limit=100"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<SearchHistoryItemDto>? history =
            await response.Content.ReadFromJsonAsync<
                IReadOnlyList<SearchHistoryItemDto>
            >();
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task CompareWithoutTokenReturns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/github/compare?users=user1,user2"
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("user1")]
    [InlineData("user1,user2,user3")]
    public async Task CompareWithInvalidUsernameCountReturns400(string users)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync(
            $"/api/github/compare?users={users}"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompareWithTokenReturns200()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken()
        );

        HttpResponseMessage response = await client.GetAsync(
            "/api/github/compare?users=user1,user2"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ProfileComparisonDto? result =
            await response.Content.ReadFromJsonAsync<ProfileComparisonDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Profiles.Count);
        Assert.Equal("user1", result.Profiles[0].Username);
        Assert.Equal("user2", result.Profiles[1].Username);
    }
}
