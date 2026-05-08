using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GitHubProfileAnalytics.Data;
using GitHubProfileAnalytics.Domain;
using GitHubProfileAnalytics.DTOs.Auth;
using GitHubProfileAnalytics.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GitHubProfileAnalytics.Services.Auth;

public class AuthService(AppDbContext context, IConfiguration configuration) : IAuthService
{
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var exists = await context.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
        {
            return null;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return await GenerateTokenPairAsync(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        return await GenerateTokenPairAsync(user);
    }

    public async Task<AuthResponse?> RefreshAsync(string token)
    {
        var refreshToken = await context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);

        if (
            refreshToken is null
            || refreshToken.RevokedAt is not null
            || refreshToken.ExpiresAt <= DateTimeOffset.UtcNow
        )
        {
            return null;
        }

        var user = await context.Users.FindAsync(refreshToken.UserId);
        if (user is null)
        {
            return null;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return await GenerateTokenPairAsync(user);
    }

    private async Task<AuthResponse> GenerateTokenPairAsync(User user)
    {
        var jwtKey = configuration.GetRequired("Jwt:Key");
        var issuer = configuration.GetRequired("Jwt:Issuer");
        var audience = configuration.GetRequired("Jwt:Audience");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
        };

        var jwtToken = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken),
            RefreshToken = refreshToken.Token,
        };
    }
}
