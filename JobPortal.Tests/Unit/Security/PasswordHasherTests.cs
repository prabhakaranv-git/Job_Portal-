using FluentAssertions;
using JobPortal.Infrastructure.Services;
using Xunit;

namespace JobPortal.Tests.Unit.Security;

public class PasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsValidHash_AndVerifiesSuccessfully()
    {
        // Arrange
        var password = "SecurePassword123!@#";

        // Act
        var hash = _hasher.HashPassword(password);
        var isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2");
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var password = "CorrectPassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var isValid = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("password", "")]
    [InlineData("", "hash")]
    public void VerifyPassword_WithEmptyInputs_ReturnsFalse(string password, string hash)
    {
        // Act
        var isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        isValid.Should().BeFalse();
    }
}
