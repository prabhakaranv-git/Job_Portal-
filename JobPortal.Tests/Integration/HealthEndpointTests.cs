using System.Net;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Health;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetApiV1Health_ReturnsSuccessAndCorrectPayload()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Request-ID");

        var json = await response.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<HealthCheckResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        health.Should().NotBeNull();
        health!.Status.Should().Be("Healthy");
        health.Service.Should().NotBeNullOrWhiteSpace();
        health.Version.Should().NotBeNullOrWhiteSpace();
        health.Database.Should().NotBeNull();
        health.Database.CanConnect.Should().BeTrue();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetHealthLive_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
