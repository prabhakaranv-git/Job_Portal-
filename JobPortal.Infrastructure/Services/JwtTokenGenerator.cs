using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace JobPortal.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenGenerator(IConfiguration configuration, IDateTimeProvider dateTimeProvider)
    {
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "DefaultFallbackSecretKeyForDevelopmentOnlyNeedsToBe32BytesLong!";
        var issuer = _configuration["Jwt:Issuer"] ?? "JobPortalAPI";
        var audience = _configuration["Jwt:Audience"] ?? "JobPortalClients";
        var expirationMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 15;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var utcNow = _dateTimeProvider.UtcNow;
        var expiresAt = utcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(token), expiresAt);
    }

    public (RefreshToken Entity, string RawToken) GenerateRefreshToken(Guid userId)
    {
        var expirationDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        var rawToken = Convert.ToBase64String(randomNumber);
        var hashedToken = HashToken(rawToken);
        var utcNow = _dateTimeProvider.UtcNow;

        var entity = new RefreshToken
        {
            Token = hashedToken,
            UserId = userId,
            ExpiresAt = utcNow.AddDays(expirationDays),
            CreatedAt = utcNow
        };

        return (entity, rawToken);
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
