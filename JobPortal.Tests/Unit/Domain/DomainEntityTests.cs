using FluentAssertions;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using Xunit;

namespace JobPortal.Tests.Unit.Domain;

public class DomainEntityTests
{
    [Fact]
    public void BaseEntity_SetsDefaultGuidId_AndUtcCreatedAt()
    {
        // Arrange & Act
        var role = new Role { Name = "Tester" };

        // Assert
        role.Id.Should().NotBeEmpty();
        role.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        role.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        role.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void RefreshToken_WhenNotExpiredAndNotRevoked_IsActiveReturnsTrue()
    {
        // Arrange
        var token = new RefreshToken
        {
            Token = "valid-token-string",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };

        // Assert
        token.IsExpired.Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_WhenExpired_IsActiveReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Token = "expired-token",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            RevokedAt = null
        };

        // Assert
        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_WhenRevoked_IsActiveReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Token = "revoked-token",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow
        };

        // Assert
        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void User_Initialization_DefaultsToActive()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "candidate@jobportal.com",
            FirstName = "Jane",
            LastName = "Doe",
            Role = UserRole.JobSeeker
        };

        // Assert
        user.IsActive.Should().BeTrue();
        user.RefreshTokens.Should().BeEmpty();
        user.Role.Should().Be(UserRole.JobSeeker);
    }
}
