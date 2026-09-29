using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Auth;

public class RefreshTokenTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RefreshTokenTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_RotatesTokenAndReturnsNewPair()
    {
        // Arrange
        var email = $"refresh_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);

        var request = new RefreshTokenRequestDto
        {
            RefreshToken = auth.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);

        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        apiResponse.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        apiResponse.Data.RefreshToken.Should().NotBe(auth.RefreshToken); // Rotated token
    }

    [Fact]
    public async Task RefreshToken_WithRotatedOldToken_DetectsReuseAndRejects()
    {
        // Arrange
        var email = $"reuse_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);
        var oldToken = auth.RefreshToken;

        // First rotation: valid
        var firstRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequestDto { RefreshToken = oldToken });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second rotation attempt with already revoked token: REUSE DETECTED
        // Act
        var reuseResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequestDto { RefreshToken = oldToken });

        // Assert
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await reuseResponse.Content.ReadAsStringAsync();
        content.Should().Contain("revoked");
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_Returns401Unauthorized()
    {
        // Arrange
        var email = $"expired_token_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);

        // Manually expire the refresh token in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = db.RefreshTokens.First(rt => rt.UserId == auth.UserId);
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequestDto { RefreshToken = auth.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("expired");
    }
}
