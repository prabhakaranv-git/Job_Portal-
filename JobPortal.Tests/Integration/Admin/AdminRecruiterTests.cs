using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Admin;
using JobPortal.Application.DTOs.Common;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Admin;

public class AdminRecruiterTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AdminRecruiterTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_CanApproveRecruiter_Successfully()
    {
        // Arrange
        var adminEmail = $"admin_appr_{Guid.NewGuid():N}@jobportal.local";
        var adminPassword = "AdminPassword123!";
        await AuthTestHelper.SeedAdminUserAsync(_factory, adminEmail, adminPassword);

        // Register a recruiter
        var recruiterEmail = $"recruiter_to_approve_{Guid.NewGuid():N}@company.com";
        var recruiterAuth = await AuthTestHelper.RegisterUserAsync(_client, recruiterEmail, "Password123!", UserRole.Recruiter, "John", "Recruiter", "Awesome Tech Inc");

        Guid recruiterId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            recruiterId = db.Recruiters.First(r => r.UserId == recruiterAuth.UserId).Id;
        }

        // Login as Admin
        var adminAuth = await AuthTestHelper.LoginUserAsync(_client, adminEmail, adminPassword);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);

        // Act: Admin approves recruiter
        var approveResponse = await _client.PutAsync($"/api/v1/admin/recruiters/{recruiterId}/approve", null);

        // Assert
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await approveResponse.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<RecruiterApprovalResponseDto>>(content, JsonOptions);

        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.IsApproved.Should().BeTrue();
        apiResponse.Data.ApprovalStatus.Should().Be("Approved");
        apiResponse.Data.ApprovedAt.Should().NotBeNull();

        // Verify recruiter can now access recruiter-protected endpoints
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);
        var recruiterDashboard = await _client.GetAsync("/api/v1/recruiter/dashboard");
        recruiterDashboard.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_CanRejectRecruiter_WithReason()
    {
        // Arrange
        var adminEmail = $"admin_rej_{Guid.NewGuid():N}@jobportal.local";
        var adminPassword = "AdminPassword123!";
        await AuthTestHelper.SeedAdminUserAsync(_factory, adminEmail, adminPassword);

        // Register a recruiter
        var recruiterEmail = $"recruiter_to_reject_{Guid.NewGuid():N}@company.com";
        var recruiterAuth = await AuthTestHelper.RegisterUserAsync(_client, recruiterEmail, "Password123!", UserRole.Recruiter, "Jane", "Recruiter");

        Guid recruiterId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            recruiterId = db.Recruiters.First(r => r.UserId == recruiterAuth.UserId).Id;
        }

        // Login as Admin
        var adminAuth = await AuthTestHelper.LoginUserAsync(_client, adminEmail, adminPassword);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);

        // Act: Admin rejects recruiter
        var rejectRequest = new RejectRecruiterRequestDto { Reason = "Company verification failed." };
        var rejectResponse = await _client.PutAsJsonAsync($"/api/v1/admin/recruiters/{recruiterId}/reject", rejectRequest);

        // Assert
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await rejectResponse.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<RecruiterApprovalResponseDto>>(content, JsonOptions);

        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.IsApproved.Should().BeFalse();
        apiResponse.Data.ApprovalStatus.Should().Be("Rejected");
        apiResponse.Data.RejectionReason.Should().Be("Company verification failed.");
        apiResponse.Data.RejectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NonAdmin_AttemptingRecruiterApproval_Returns403Forbidden()
    {
        // Arrange: Logged in as JobSeeker
        var seekerEmail = $"seeker_unauth_{Guid.NewGuid():N}@example.com";
        var seekerAuth = await AuthTestHelper.RegisterUserAsync(_client, seekerEmail, "Password123!", UserRole.JobSeeker);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        // Act
        var response = await _client.PutAsync($"/api/v1/admin/recruiters/{Guid.NewGuid()}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ApprovingNonExistentRecruiter_Returns404NotFound()
    {
        // Arrange
        var adminEmail = $"admin_404_{Guid.NewGuid():N}@jobportal.local";
        var adminPassword = "AdminPassword123!";
        await AuthTestHelper.SeedAdminUserAsync(_factory, adminEmail, adminPassword);

        var adminAuth = await AuthTestHelper.LoginUserAsync(_client, adminEmail, adminPassword);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);

        // Act
        var response = await _client.PutAsync($"/api/v1/admin/recruiters/{Guid.NewGuid()}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
