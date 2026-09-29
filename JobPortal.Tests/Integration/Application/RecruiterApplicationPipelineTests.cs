using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Application;

public class RecruiterApplicationPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RecruiterApplicationPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(AuthResponseDto RecruiterAuth, Guid JobId, ApplicationResponse Application)> SetupApplicationAsync(string companyName = "PipelineCorp")
    {
        // 1. Recruiter creates & publishes job
        var (recruiterAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName);
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        var createJobRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = $"Lead Cloud Engineer {Guid.NewGuid():N}",
            Description = "Develop and orchestrate distributed cloud infrastructure.",
            Location = "Hyderabad, India",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        var job = (await createJobRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);

        // 2. JobSeeker applies
        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        var form = new MultipartFormDataContent();
        var fileBytes = "%PDF-1.4 Applicant CV Sample Content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "applicant_cv.pdf");

        var resumeRes = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        var resume = (await resumeRes.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        var applyRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{job.Id}/apply", new ApplyJobRequest
        {
            ResumeId = resume.Id,
            CoverLetter = "Excited to apply for this lead role."
        });
        var application = (await applyRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        return (recruiterAuth, job.Id, application);
    }

    [Fact]
    public async Task ApprovedRecruiter_ListsJobApplications_AndGetsApplicationDetail()
    {
        var (recruiterAuth, jobId, application) = await SetupApplicationAsync("Recruiter Pipeline Alpha");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // 1. List applications for the job
        var listRes = await _client.GetAsync($"/api/v1/recruiter/jobs/{jobId}/applications?page=1&pageSize=20&status=Submitted");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = (await listRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ApplicationListItemResponse>>>(JsonOptions))!.Data!;
        paged.Items.Should().Contain(a => a.Id == application.Id);
        paged.Items.First(a => a.Id == application.Id).ResumeFileName.Should().Be("applicant_cv.pdf");

        // 2. Get application by ID
        var detailRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await detailRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationListItemResponse>>(JsonOptions))!.Data!;
        detail.Id.Should().Be(application.Id);
        detail.CoverLetter.Should().Be("Excited to apply for this lead role.");
    }

    [Fact]
    public async Task ApprovedRecruiter_DownloadsApplicantResume()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Resume Download Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        var response = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/resume");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApprovedRecruiter_ProgressesApplicationThroughFullPipeline()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Pipeline Progression Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // 1. Submitted -> UnderReview
        var res1 = await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.UnderReview });
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res1.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!.Status.Should().Be(ApplicationStatus.UnderReview);

        // 2. UnderReview -> Shortlisted
        var res2 = await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.Shortlisted });
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res2.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!.Status.Should().Be(ApplicationStatus.Shortlisted);

        // 3. Shortlisted -> Interviewing
        var res3 = await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.Interviewing });
        res3.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res3.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!.Status.Should().Be(ApplicationStatus.Interviewing);

        // 4. Interviewing -> Accepted
        var res4 = await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.Accepted });
        res4.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res4.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!.Status.Should().Be(ApplicationStatus.Accepted);

        // 5. Accepted is terminal -> any transition returns 409 Conflict
        var res5 = await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.Rejected });
        res5.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RecruiterA_CannotAccessOrModify_RecruiterB_Applications()
    {
        var (_, jobIdA, applicationA) = await SetupApplicationAsync("Recruiter Alpha Corp");

        // Recruiter B is authenticated
        var (recruiterBAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "Recruiter Beta Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterBAuth.AccessToken);

        // Recruiter B cannot list applications for Job A
        (await _client.GetAsync($"/api/v1/recruiter/jobs/{jobIdA}/applications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Recruiter B cannot view application A
        (await _client.GetAsync($"/api/v1/recruiter/applications/{applicationA.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Recruiter B cannot download resume for application A
        (await _client.GetAsync($"/api/v1/recruiter/applications/{applicationA.Id}/resume")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Recruiter B cannot update status for application A
        (await _client.PatchAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/status", new UpdateApplicationStatusRequest { Status = ApplicationStatus.Shortlisted }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnapprovedRecruiter_AccessingRecruiterApplicationEndpoints_Returns403Forbidden()
    {
        var unapprovedAuth = await AuthTestHelper.CreateUnapprovedRecruiterAsync(_client, "PendingCorp");
        AuthTestHelper.SetBearerToken(_client, unapprovedAuth.AccessToken);

        (await _client.GetAsync($"/api/v1/recruiter/jobs/{Guid.NewGuid()}/applications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/v1/recruiter/applications/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
