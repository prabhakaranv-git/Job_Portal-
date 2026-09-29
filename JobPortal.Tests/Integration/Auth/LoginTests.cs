using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Auth;

public class LoginTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public LoginTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndValidJwtWithClaims()
    {
        // Arrange
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        await AuthTestHelper.RegisterUserAsync(_client, email, password, UserRole.JobSeeker, "Alice", "Smith");

        var loginRequest = new LoginRequestDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);

        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        apiResponse.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        apiResponse.Data.Role.Should().Be("JobSeeker");

        // Validate JWT Claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(apiResponse.Data.AccessToken);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email.ToLower());
        jwt.Claims.Should().Contain(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "JobSeeker");
        jwt.Claims.Should().Contain(c => c.Type == "firstName" && c.Value == "Alice");
        jwt.Claims.Should().Contain(c => c.Type == "lastName" && c.Value == "Smith");
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_Returns401Unauthorized()
    {
        // Arrange
        var email = $"wrong_pw_{Guid.NewGuid():N}@example.com";
        await AuthTestHelper.RegisterUserAsync(_client, email, "CorrectPassword123!", UserRole.JobSeeker);

        var loginRequest = new LoginRequestDto
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401WithGenericMessage()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            Email = "nonexistent_email_12345@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        // Generic message prevents user enumeration
        content.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401Unauthorized()
    {
        // Arrange
        var email = $"inactive_{Guid.NewGuid():N}@example.com";
        var password = "Password123!";
        var user = await AuthTestHelper.RegisterUserAsync(_client, email, password, UserRole.JobSeeker);

        // Manually deactivate user in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = db.Users.First(u => u.Id == user.UserId);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        var loginRequest = new LoginRequestDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("inactive");
    }
}
