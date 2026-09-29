using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.CandidateFeedback;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Recruiter;

public class CandidateFeedbackTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CandidateFeedbackTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(AuthResponseDto RecruiterAuth, Guid JobId, ApplicationResponse Application)> SetupApplicationAsync(string companyName = "FeedbackCorp")
    {
        // 1. Recruiter creates & publishes job
        var (recruiterAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName);
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        var createJobRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = $"Staff Software Engineer {Guid.NewGuid():N}",
            Description = "Lead backend architecture and engineering excellence.",
            Location = "Remote, India",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        var job = (await createJobRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);

        // 2. JobSeeker applies
        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        var form = new MultipartFormDataContent();
        var fileBytes = "%PDF-1.4 Candidate Profile"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "candidate_resume.pdf");

        var resumeRes = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        var resume = (await resumeRes.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        var applyRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{job.Id}/apply", new ApplyJobRequest
        {
            ResumeId = resume.Id,
            CoverLetter = "Strong background in cloud distributed systems."
        });
        var application = (await applyRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        return (recruiterAuth, job.Id, application);
    }

    [Fact]
    public async Task ApprovedRecruiter_CanCreate_Get_Update_And_Delete_Feedback()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Feedback Lifecycle Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // 1. Check GET before feedback created -> 404
        var getNotFound = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/feedback");
        getNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Create Candidate Feedback
        var createRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Strengths = "Excellent understanding of asynchronous programming, clean architecture, and PostgreSQL.",
            Weaknesses = "Could strengthen experience in Kubernetes operators.",
            Recommendation = FeedbackRecommendation.Hire,
            DetailedFeedback = "Very strong technical round. Recommend moving forward to bar raiser."
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFeedback = (await createRes.Content.ReadFromJsonAsync<ApiResponse<CandidateFeedbackResponse>>(JsonOptions))!.Data!;
        createdFeedback.ApplicationId.Should().Be(application.Id);
        createdFeedback.OverallRating.Should().Be(4);
        createdFeedback.Recommendation.Should().Be(FeedbackRecommendation.Hire);
        createdFeedback.Strengths.Should().Be("Excellent understanding of asynchronous programming, clean architecture, and PostgreSQL.");
        createdFeedback.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // 3. Attempt Duplicate Creation -> 409 Conflict
        var dupRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 5,
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Duplicate attempt"
        });
        dupRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. Get Feedback
        var getRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/feedback");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedFeedback = (await getRes.Content.ReadFromJsonAsync<ApiResponse<CandidateFeedbackResponse>>(JsonOptions))!.Data!;
        fetchedFeedback.Id.Should().Be(createdFeedback.Id);
        fetchedFeedback.OverallRating.Should().Be(4);

        // 5. Update Feedback
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new UpdateCandidateFeedbackRequest
        {
            OverallRating = 5,
            Strengths = "Outstanding performance in system design and database indexing.",
            Weaknesses = "None observed.",
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Exceptional senior candidate. Recommend immediate offer."
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedFeedback = (await updateRes.Content.ReadFromJsonAsync<ApiResponse<CandidateFeedbackResponse>>(JsonOptions))!.Data!;
        updatedFeedback.OverallRating.Should().Be(5);
        updatedFeedback.Recommendation.Should().Be(FeedbackRecommendation.StrongHire);
        updatedFeedback.UpdatedAt.Should().NotBeNull();

        // 6. Delete Feedback
        var deleteRes = await _client.DeleteAsync($"/api/v1/recruiter/applications/{application.Id}/feedback");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. GET after delete -> 404
        var getAfterDelete = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/feedback");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateFeedback_ValidationRules_Enforced()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Feedback Validation Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // Rating < 1
        var resLowRating = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 0,
            Recommendation = FeedbackRecommendation.NoHire,
            DetailedFeedback = "Too low rating test."
        });
        resLowRating.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Rating > 5
        var resHighRating = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 6,
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Too high rating test."
        });
        resHighRating.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Completely empty text fields
        var resEmptyText = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 3,
            Recommendation = FeedbackRecommendation.Maybe,
            Strengths = "   ",
            Weaknesses = "",
            DetailedFeedback = null
        });
        resEmptyText.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Strengths > 3000 chars
        var resLongStrengths = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            Strengths = new string('S', 3001)
        });
        resLongStrengths.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Weaknesses > 3000 chars
        var resLongWeaknesses = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            Weaknesses = new string('W', 3001)
        });
        resLongWeaknesses.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // DetailedFeedback > 5000 chars
        var resLongDetails = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            DetailedFeedback = new string('D', 5001)
        });
        resLongDetails.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrossTenant_RecruiterBCannotAccessOrModify_RecruiterAFeedback()
    {
        var (recruiterAAuth, _, applicationA) = await SetupApplicationAsync("Feedback Tenant A");
        AuthTestHelper.SetBearerToken(_client, recruiterAAuth.AccessToken);

        // Recruiter A creates feedback
        await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            DetailedFeedback = "Recruiter A private feedback."
        });

        // Recruiter B logs in
        var (recruiterBAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "Feedback Tenant B");
        AuthTestHelper.SetBearerToken(_client, recruiterBAuth.AccessToken);

        // 1. Recruiter B tries to get Feedback for Application A -> 403 Forbidden
        var getB = await _client.GetAsync($"/api/v1/recruiter/applications/{applicationA.Id}/feedback");
        getB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Recruiter B tries to create Feedback for Application A -> 403 Forbidden
        var createB = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 3,
            Recommendation = FeedbackRecommendation.Maybe,
            DetailedFeedback = "Unauthorized attempt."
        });
        createB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Recruiter B tries to update Feedback for Application A -> 403 Forbidden
        var updateB = await _client.PutAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/feedback", new UpdateCandidateFeedbackRequest
        {
            OverallRating = 5,
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Unauthorized update."
        });
        updateB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Recruiter B tries to delete Feedback for Application A -> 403 Forbidden
        var delB = await _client.DeleteAsync($"/api/v1/recruiter/applications/{applicationA.Id}/feedback");
        delB.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JobSeeker_CannotAccessCandidateFeedback()
    {
        var (_, _, application) = await SetupApplicationAsync("Feedback JobSeeker Isolation Corp");

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        var getRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/feedback");
        getRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/feedback", new CreateCandidateFeedbackRequest
        {
            OverallRating = 5,
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Seeker attempting feedback creation"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnapprovedRecruiter_CannotAccessCandidateFeedback()
    {
        var unapprovedAuth = await AuthTestHelper.CreateUnapprovedRecruiterAsync(_client, "PendingFeedbackCorp");
        AuthTestHelper.SetBearerToken(_client, unapprovedAuth.AccessToken);

        var getRes = await _client.GetAsync($"/api/v1/recruiter/applications/{Guid.NewGuid()}/feedback");
        getRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessCandidateFeedback()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var getRes = await _client.GetAsync($"/api/v1/recruiter/applications/{Guid.NewGuid()}/feedback");
        getRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
