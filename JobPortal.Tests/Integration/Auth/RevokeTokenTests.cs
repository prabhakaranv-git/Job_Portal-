using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Auth;

public class RevokeTokenTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RevokeTokenTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RevokeToken_WithValidToken_Returns200AndInvalidatesToken()
    {
        // Arrange
        var email = $"revoke_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);

        var request = new RevokeTokenRequestDto
        {
            RefreshToken = auth.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/revoke-token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Attempt to refresh with the revoked token -> should fail
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequestDto { RefreshToken = auth.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public class ChangePasswordTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ChangePasswordTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_UpdatesPasswordAndRevokesSessions()
    {
        // Arrange
        var email = $"chg_pwd_{Guid.NewGuid():N}@example.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword123!@#";

        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, oldPassword, UserRole.JobSeeker);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = oldPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 1. Old refresh token should be revoked
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequestDto { RefreshToken = auth.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Login with old password should fail
        var oldLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = oldPassword });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 3. Login with new password should succeed
        var newLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = newPassword });
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400BadRequest()
    {
        // Arrange
        var email = $"chg_wrong_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "ActualPassword123!", UserRole.JobSeeker);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "WrongCurrentPassword123!",
            NewPassword = "BrandNewPassword123!",
            ConfirmNewPassword = "BrandNewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Current password is incorrect");
    }

    [Fact]
    public async Task ChangePassword_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmNewPassword = "NewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
