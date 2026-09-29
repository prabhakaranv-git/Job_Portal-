using System.Net;
using FluentAssertions;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Authorization;

public class AuthorizationMatrixTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationMatrixTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_AccessingProtectedEndpoints_Returns401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act & Assert
        (await _client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/v1/jobseeker/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/v1/recruiter/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/v1/admin/recruiters/pending")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JobSeeker_AccessingJobSeekerEndpoint_Returns200OK()
    {
        // Arrange
        var email = $"seeker_auth_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1/jobseeker/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JobSeeker_AccessingRecruiterOrAdminEndpoints_Returns403Forbidden()
    {
        // Arrange
        var email = $"seeker_cross_{Guid.NewGuid():N}@example.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.JobSeeker);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Act & Assert
        (await _client.GetAsync("/api/v1/recruiter/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/v1/admin/recruiters/pending")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnapprovedRecruiter_AccessingRecruiterDashboard_Returns403Forbidden()
    {
        // Arrange: Newly registered recruiter is Unapproved by default
        var email = $"unapproved_{Guid.NewGuid():N}@company.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.Recruiter, companyName: "TechCorp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Act: Attempt to access recruiter-protected business endpoint
        var response = await _client.GetAsync("/api/v1/recruiter/dashboard");

        // Assert: 403 Forbidden because Recruiter.IsApproved is false!
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApprovedRecruiter_AccessingRecruiterDashboard_Returns200OK()
    {
        // Arrange: Register recruiter then approve in DB
        var email = $"approved_{Guid.NewGuid():N}@company.com";
        var auth = await AuthTestHelper.RegisterUserAsync(_client, email, "Password123!", UserRole.Recruiter, companyName: "TechCorp");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recruiter = db.Recruiters.First(r => r.UserId == auth.UserId);
            recruiter.IsApproved = true;
            recruiter.ApprovalStatus = RecruiterApprovalStatus.Approved;
            await db.SaveChangesAsync();
        }

        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1/recruiter/dashboard");

        // Assert: 200 OK after approval
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_AccessingAdminEndpoint_Returns200OK()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid():N}@jobportal.local";
        var password = "AdminPassword123!";
        await AuthTestHelper.SeedAdminUserAsync(_factory, adminEmail, password);

        var auth = await AuthTestHelper.LoginUserAsync(_client, adminEmail, password);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/recruiters/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
