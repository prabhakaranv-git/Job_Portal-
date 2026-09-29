using System.Net;
using FluentAssertions;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration;

public class MiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MiddlewareTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WithCustomXRequestId_EchoesBackSameHeader()
    {
        // Arrange
        var customRequestId = "test-request-id-12345";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.Add("X-Request-ID", customRequestId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("X-Request-ID");
        response.Headers.GetValues("X-Request-ID").First().Should().Be(customRequestId);
    }

    [Fact]
    public async Task Request_WithoutCustomXRequestId_GeneratesAndReturnsNewHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.Headers.Should().ContainKey("X-Request-ID");
        response.Headers.GetValues("X-Request-ID").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Request_ContainsStandardSecurityHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");

        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.GetValues("Referrer-Policy").First().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Request_ToNonExistentEndpoint_ReturnsProblemDetailsOrNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/non-existent-route-xyz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
