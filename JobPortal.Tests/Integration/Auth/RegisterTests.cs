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

public class RegisterTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RegisterTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidJobSeekerPayload_Returns200AndTokens()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = $"seeker_{Guid.NewGuid():N}@example.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
            FirstName = "Jane",
            LastName = "Seeker",
            PhoneNumber = "+1234567890",
            Role = UserRole.JobSeeker
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Email.Should().Be(request.Email.ToLower());
        apiResponse.Data.Role.Should().Be("JobSeeker");
        apiResponse.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        apiResponse.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_WithValidRecruiterPayload_Returns200WithUnapprovedStatus()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = $"recruiter_{Guid.NewGuid():N}@company.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
            FirstName = "Alex",
            LastName = "Recruiter",
            Role = UserRole.Recruiter,
            CompanyName = "Acme Corp",
            Position = "Senior Talent Acquisition"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);

        apiResponse!.Data!.Role.Should().Be("Recruiter");
        apiResponse.Data.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        // Arrange
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequestDto
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "First",
            LastName = "User",
            Role = UserRole.JobSeeker
        };

        // First registration
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second registration with same email (case variation)
        var secondRequest = new RegisterRequestDto
        {
            Email = email.ToUpper(),
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "Second",
            LastName = "User",
            Role = UserRole.JobSeeker
        };

        // Act
        var secondResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", secondRequest);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await secondResponse.Content.ReadAsStringAsync();
        content.Should().Contain("already exists");
    }

    [Fact]
    public async Task Register_AttemptingAdminRole_Returns400BadRequest()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = $"admin_attempt_{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "Hacker",
            LastName = "Admin",
            Role = UserRole.Admin
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Admin accounts cannot be created");
    }

    [Theory]
    [InlineData("weak")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoNumberSpecial!")]
    [InlineData("NoSpecialChar123")]
    public async Task Register_WithWeakPassword_Returns400BadRequest(string password)
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = $"weak_{Guid.NewGuid():N}@example.com",
            Password = password,
            ConfirmPassword = password,
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.JobSeeker
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
