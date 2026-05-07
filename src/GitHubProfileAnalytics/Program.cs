using System.Text;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Exceptions;
using GitHubProfileAnalytics.Extensions;
using GitHubProfileAnalytics.Services.Analytics;
using GitHubProfileAnalytics.Services.Auth;
using GitHubProfileAnalytics.Services.GitHub;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);
builder.Services.AddScoped<IAuthService, AuthService>();

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration.GetRequired("Jwt:Issuer"),
            ValidAudience = builder.Configuration.GetRequired("Jwt:Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration.GetRequired("Jwt:Key"))
            ),
        };
    });

builder.Services.AddSingleton<IGitHubClient>(sp =>
{
    var token = sp.GetRequiredService<IConfiguration>().GetRequired("GitHub:Token");
    return new GitHubClient(new ProductHeaderValue("GitHubProfileAnalytics"))
    {
        Credentials = new Credentials(token),
    };
});

builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IProfileCacheService, ProfileCacheService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IAnalyticsCacheService, AnalyticsCacheService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
