using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Job;

public class JobLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public JobLifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateSkillAsync(string name)
    {
        var adminAuth = await AuthTestHelper.CreateAdminAsync(_client, _factory);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);
        var res = await _client.PostAsJsonAsync("/api/v1/skills", new CreateSkillRequest { Name = name });
        var data = (await res.Content.ReadFromJsonAsync<ApiResponse<SkillResponse>>(JsonOptions))!.Data!;
        return data.Id;
    }

    [Fact]
    public async Task ApprovedRecruiter_CreatesDraftJob_Returns201Created()
    {
        // Arrange
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Tech Giants");
        var skillId = await CreateSkillAsync($"Skill_{Guid.NewGuid():N}");

        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CreateJobRequest
        {
            Title = "Senior Backend Engineer",
            Description = "We are seeking a senior backend engineer proficient with .NET Core and PostgreSQL.",
            Location = "Bangalore, India",
            JobType = JobType.FullTime,
            SalaryMin = 80000,
            SalaryMax = 120000,
            Currency = "USD",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            SkillIds = new List<Guid> { skillId }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Title.Should().Be(request.Title);
        result.Data.Status.Should().Be(JobStatus.Draft);
        result.Data.Skills.Should().ContainSingle(s => s.Id == skillId);
    }

    [Fact]
    public async Task UnapprovedRecruiter_CreatingJob_Returns403Forbidden()
    {
        var auth = await AuthTestHelper.CreateUnapprovedRecruiterAsync(_client, companyName: "SomeCorp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CreateJobRequest
        {
            Title = "Backend Dev",
            Description = "Description with sufficient length for validation.",
            Location = "Remote",
            JobType = JobType.FullTime
        };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JobSeeker_CreatingJob_Returns403Forbidden()
    {
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CreateJobRequest
        {
            Title = "Backend Dev",
            Description = "Description with sufficient length for validation.",
            Location = "Remote",
            JobType = JobType.FullTime
        };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Recruiter_WithoutCompany_CreatingJob_Returns409Conflict()
    {
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CreateJobRequest
        {
            Title = "Backend Dev",
            Description = "Description with sufficient length for validation.",
            Location = "Remote",
            JobType = JobType.FullTime
        };

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JobLifecycle_Draft_To_Published_To_Closed_To_Archived()
    {
        // Arrange: Create Draft Job
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Lifecycle Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Full Stack Engineer",
            Description = "Build scalable web applications with ASP.NET Core.",
            Location = "Chennai, India",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(15)
        });
        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        job.Status.Should().Be(JobStatus.Draft);

        // Act 1: Publish Job
        var pubRes = await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);
        pubRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pubJob = (await pubRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        pubJob.Status.Should().Be(JobStatus.Published);

        // Act 2: Close Job
        var closeRes = await _client.PutAsync($"/api/v1/jobs/{job.Id}/close", null);
        closeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var closeJob = (await closeRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        closeJob.Status.Should().Be(JobStatus.Closed);

        // Act 3: Archive Job
        var archiveRes = await _client.PutAsync($"/api/v1/jobs/{job.Id}/archive", null);
        archiveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var archiveJob = (await archiveRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        archiveJob.Status.Should().Be(JobStatus.Archived);
    }

    [Fact]
    public async Task JobLifecycle_Draft_To_Archived()
    {
        // Arrange: Create Draft Job
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Archive Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "DevOps Engineer",
            Description = "Manage cloud infrastructure and CI/CD pipelines.",
            Location = "Remote",
            JobType = JobType.Contract
        });
        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Act: Directly archive draft job
        var archiveRes = await _client.PutAsync($"/api/v1/jobs/{job.Id}/archive", null);
        archiveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var archiveJob = (await archiveRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        archiveJob.Status.Should().Be(JobStatus.Archived);
    }

    [Fact]
    public async Task InvalidTransitions_Return409Conflict()
    {
        // Arrange
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Transition Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "QA Automation Lead",
            Description = "Design end-to-end automated testing suites.",
            Location = "Remote",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(10)
        });
        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Cannot close a Draft job
        (await _client.PutAsync($"/api/v1/jobs/{job.Id}/close", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Publish to make it Published
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);

        // Cannot publish an already Published job
        (await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Close job
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/close", null);

        // Cannot publish a Closed job
        (await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Archive job
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/archive", null);

        // Cannot archive an already Archived job
        (await _client.PutAsync($"/api/v1/jobs/{job.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Cannot publish an Archived job
        (await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_JobWithPastExpiration_Returns409Conflict()
    {
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Exp Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Create job with draft
        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Expired Job Test",
            Description = "This job will be manually set with an expired date.",
            Location = "Remote",
            JobType = JobType.FullTime
        });
        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Simulate expiration in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPortal.Infrastructure.Data.ApplicationDbContext>();
            var dbJob = db.Jobs.First(j => j.Id == job.Id);
            dbJob.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        // Act: Attempt to publish expired job
        var pubRes = await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);

        // Assert
        pubRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
