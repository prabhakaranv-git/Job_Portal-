using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using JobPortal.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobPortal.Tests.Integration.Application;

public class JobApplicationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public JobApplicationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid JobId, Guid RecruiterId)> CreateAndPublishJobAsync(string companyName = "AppTestCorp")
    {
        var (auth, recruiterId) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = $"Software Engineer {Guid.NewGuid():N}",
            Description = "Join our engineering team building high performance microservices.",
            Location = "Bangalore, India",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);
        return (job.Id, recruiterId);
    }

    private async Task<Guid> UploadResumeForJobSeekerAsync()
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("%PDF-1.4 sample resume document"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "candidate_resume.pdf");

        var response = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions);
        return result!.Data!.Id;
    }

    [Fact]
    public async Task JobSeeker_AppliesToPublishedJob_Returns200OK_StatusSubmitted()
    {
        // 1. Recruiter creates & publishes job
        var (jobId, _) = await CreateAndPublishJobAsync("Alpha Innovations");

        // 2. Job Seeker uploads resume & applies
        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var applyRequest = new ApplyJobRequest
        {
            ResumeId = resumeId,
            CoverLetter = "I have 5+ years of experience with .NET Core and PostgreSQL."
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", applyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.JobId.Should().Be(jobId);
        result.Data.ResumeId.Should().Be(resumeId);
        result.Data.Status.Should().Be(ApplicationStatus.Submitted);
        result.Data.CoverLetter.Should().Be(applyRequest.CoverLetter);
    }

    [Fact]
    public async Task JobSeeker_ApplyingTwiceToSameJob_Returns409Conflict()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("Beta Technologies");

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var applyRequest = new ApplyJobRequest { ResumeId = resumeId };

        // 1. First application succeeds
        var firstRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", applyRequest);
        firstRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Second application rejected as duplicate
        var secondRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", applyRequest);
        secondRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JobSeeker_ApplyingToDraftJob_Returns409Conflict()
    {
        // Create draft job (never published)
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "Draft Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);
        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Draft Role",
            Description = "Description for draft posting.",
            Location = "Remote",
            JobType = JobType.FullTime
        });
        var draftJob = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Job Seeker attempts to apply
        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{draftJob.Id}/apply", new ApplyJobRequest { ResumeId = resumeId });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JobSeeker_ApplyingToExpiredJob_Returns409Conflict()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("Expired Job Corp");

        // Manually expire the job in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = db.Jobs.First(j => j.Id == jobId);
            job.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", new ApplyJobRequest { ResumeId = resumeId });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JobSeeker_ApplyingWithAnotherSeekerResume_Returns404NotFound()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("Cross Resume Corp");

        // JobSeeker 1 uploads resume
        var seeker1 = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seeker1.AccessToken);
        var resume1 = await UploadResumeForJobSeekerAsync();

        // JobSeeker 2 attempts to use JobSeeker 1's resume
        var seeker2 = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seeker2.AccessToken);

        var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", new ApplyJobRequest { ResumeId = resume1 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JobSeeker_ViewsApplicationHistory_AndDetails()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("History Corp");

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var applyRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", new ApplyJobRequest { ResumeId = resumeId, CoverLetter = "Hello" });
        var app = (await applyRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        // 1. History query
        var historyRes = await _client.GetAsync("/api/v1/jobseeker/applications?page=1&pageSize=10");
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = (await historyRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ApplicationListItemResponse>>>(JsonOptions))!.Data!;
        history.Items.Should().Contain(a => a.Id == app.Id);

        // 2. Application detail by ID
        var detailRes = await _client.GetAsync($"/api/v1/jobseeker/applications/{app.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await detailRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;
        detail.Id.Should().Be(app.Id);
        detail.Status.Should().Be(ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task JobSeeker_WithdrawsApplication_Successfully()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("Withdraw Corp");

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);
        var resumeId = await UploadResumeForJobSeekerAsync();

        var applyRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", new ApplyJobRequest { ResumeId = resumeId });
        var app = (await applyRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        // Act: Withdraw
        var withdrawRes = await _client.PatchAsync($"/api/v1/jobseeker/applications/{app.Id}/withdraw", null);

        // Assert
        withdrawRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await withdrawRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;
        result.Status.Should().Be(ApplicationStatus.Withdrawn);

        // Attempting to withdraw an already withdrawn application returns 409 Conflict
        var secondWithdraw = await _client.PatchAsync($"/api/v1/jobseeker/applications/{app.Id}/withdraw", null);
        secondWithdraw.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JobSeekerA_CannotViewOrWithdraw_JobSeekerB_Application()
    {
        var (jobId, _) = await CreateAndPublishJobAsync("Isolation Corp");

        // Seeker A applies
        var seekerA = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerA.AccessToken);
        var resumeA = await UploadResumeForJobSeekerAsync();
        var appA = (await (await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/apply", new ApplyJobRequest { ResumeId = resumeA })).Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        // Seeker B attempts access
        var seekerB = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerB.AccessToken);

        (await _client.GetAsync($"/api/v1/jobseeker/applications/{appA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PatchAsync($"/api/v1/jobseeker/applications/{appA.Id}/withdraw", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
