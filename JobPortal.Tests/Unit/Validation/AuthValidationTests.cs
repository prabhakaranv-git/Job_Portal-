using FluentAssertions;
using FluentValidation.TestHelper;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.Validators.Auth;
using JobPortal.Domain.Enums;
using Xunit;

namespace JobPortal.Tests.Unit.Validation;

public class AuthValidationTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();
    private readonly RefreshTokenRequestValidator _refreshTokenValidator = new();

    [Fact]
    public void RegisterValidator_WithValidData_ShouldNotHaveValidationError()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "john.doe@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.JobSeeker
        };

        // Act
        var result = _registerValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RegisterValidator_WithAdminRole_ShouldHaveValidationError()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "admin@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "Admin",
            LastName = "User",
            Role = UserRole.Admin
        };

        // Act
        var result = _registerValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("@missingusername.com")]
    public void RegisterValidator_WithInvalidEmail_ShouldHaveValidationError(string email)
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.JobSeeker
        };

        // Act
        var result = _registerValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterValidator_WhenPasswordsDoNotMatch_ShouldHaveValidationError()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "john.doe@example.com",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.JobSeeker
        };

        // Act
        var result = _registerValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void LoginValidator_WithEmptyCredentials_ShouldHaveValidationErrors()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "",
            Password = ""
        };

        // Act
        var result = _loginValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ChangePasswordValidator_WithValidPasswords_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!@#",
            ConfirmNewPassword = "NewPassword123!@#"
        };

        // Act
        var result = _changePasswordValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangePasswordValidator_WhenNewPasswordEqualsCurrent_ShouldHaveValidationError()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "SamePassword123!",
            NewPassword = "SamePassword123!",
            ConfirmNewPassword = "SamePassword123!"
        };

        // Act
        var result = _changePasswordValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void RefreshTokenValidator_WithEmptyToken_ShouldHaveValidationError()
    {
        // Arrange
        var request = new RefreshTokenRequestDto { RefreshToken = "" };

        // Act
        var result = _refreshTokenValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
