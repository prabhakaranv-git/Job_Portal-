using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace JobPortal.Tests.Unit.Security;

public class JwtTokenGeneratorTests
{
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly IConfiguration _configuration;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly DateTime _currentUtcTime;

    public JwtTokenGeneratorTests()
    {
        _currentUtcTime = DateTime.UtcNow;
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_currentUtcTime);

        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "SuperSecretKeyForTestingPurposesMustBeAtLeast32BytesLong!" },
            { "Jwt:Issuer", "JobPortalTestIssuer" },
            { "Jwt:Audience", "JobPortalTestAudience" },
            { "Jwt:AccessTokenExpirationMinutes", "30" },
            { "Jwt:RefreshTokenExpirationDays", "14" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _tokenGenerator = new JwtTokenGenerator(_configuration, _dateTimeProviderMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt_WithExpectedClaimsAndExpiry()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "recruiter@company.com",
            FirstName = "Alex",
            LastName = "Morgan",
            Role = UserRole.Recruiter
        };

        // Act
        var (token, expiresAt) = _tokenGenerator.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        expiresAt.Should().BeCloseTo(_currentUtcTime.AddMinutes(30), TimeSpan.FromSeconds(1));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("JobPortalTestIssuer");
        jwt.Audiences.Should().Contain("JobPortalTestAudience");
        jwt.Claims.Should().Contain(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "Recruiter");
        jwt.Claims.Should().Contain(c => (c.Type == "email" || c.Type == JwtRegisteredClaimNames.Email || c.Type == ClaimTypes.Email) && c.Value == "recruiter@company.com");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsValidTokenWithExpirationAndHash()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var (entity, rawToken) = _tokenGenerator.GenerateRefreshToken(userId);

        // Assert
        entity.Should().NotBeNull();
        rawToken.Should().NotBeNullOrWhiteSpace();
        entity.UserId.Should().Be(userId);
        entity.Token.Should().NotBeNullOrWhiteSpace();
        entity.Token.Should().NotBe(rawToken); // Hashed token must not equal raw token
        entity.Token.Should().Be(_tokenGenerator.HashToken(rawToken));
        entity.ExpiresAt.Should().BeCloseTo(_currentUtcTime.AddDays(14), TimeSpan.FromSeconds(1));
        entity.CreatedAt.Should().BeCloseTo(_currentUtcTime, TimeSpan.FromSeconds(1));
        entity.IsActive.Should().BeTrue();
    }
}
