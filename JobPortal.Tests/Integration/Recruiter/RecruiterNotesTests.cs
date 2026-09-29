using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.RecruiterNote;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Recruiter;

public class RecruiterNotesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RecruiterNotesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(AuthResponseDto RecruiterAuth, Guid JobId, ApplicationResponse Application)> SetupApplicationAsync(string companyName = "NotesCorp")
    {
        // 1. Recruiter creates & publishes job
        var (recruiterAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName);
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        var createJobRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = $"Senior Backend Developer {Guid.NewGuid():N}",
            Description = "Develop high-scale backend services using .NET.",
            Location = "Bengaluru, India",
            JobType = JobType.FullTime,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        var job = (await createJobRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);

        // 2. JobSeeker applies
        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        var form = new MultipartFormDataContent();
        var fileBytes = "%PDF-1.4 Resume Content Sample"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "candidate_cv.pdf");

        var resumeRes = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        var resume = (await resumeRes.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        var applyRes = await _client.PostAsJsonAsync($"/api/v1/jobs/{job.Id}/apply", new ApplyJobRequest
        {
            ResumeId = resume.Id,
            CoverLetter = "Extensive backend experience in distributed systems."
        });
        var application = (await applyRes.Content.ReadFromJsonAsync<ApiResponse<ApplicationResponse>>(JsonOptions))!.Data!;

        return (recruiterAuth, job.Id, application);
    }

    [Fact]
    public async Task ApprovedRecruiter_CanCreate_List_Get_Update_And_Delete_Notes()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Notes LifeCycle Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // 1. Create Note 1
        var createRes1 = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "Candidate showed deep knowledge in concurrency and database transactions."
        });
        createRes1.StatusCode.Should().Be(HttpStatusCode.Created);
        var note1 = (await createRes1.Content.ReadFromJsonAsync<ApiResponse<RecruiterNoteResponse>>(JsonOptions))!.Data!;
        note1.ApplicationId.Should().Be(application.Id);
        note1.Content.Should().Be("Candidate showed deep knowledge in concurrency and database transactions.");
        note1.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // 2. Create Note 2
        var createRes2 = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "Follow-up discussion scheduled for architectural review."
        });
        createRes2.StatusCode.Should().Be(HttpStatusCode.Created);
        var note2 = (await createRes2.Content.ReadFromJsonAsync<ApiResponse<RecruiterNoteResponse>>(JsonOptions))!.Data!;

        // 3. List Notes (paginated)
        var listRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/notes?page=1&pageSize=10");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedNotes = (await listRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<RecruiterNoteResponse>>>(JsonOptions))!.Data!;
        pagedNotes.TotalCount.Should().Be(2);
        pagedNotes.Items.Should().HaveCount(2);
        pagedNotes.Items[0].Id.Should().Be(note2.Id); // Descending order
        pagedNotes.Items[1].Id.Should().Be(note1.Id);

        // 4. Get Note by ID
        var getRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/notes/{note1.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedNote = (await getRes.Content.ReadFromJsonAsync<ApiResponse<RecruiterNoteResponse>>(JsonOptions))!.Data!;
        fetchedNote.Id.Should().Be(note1.Id);
        fetchedNote.Content.Should().Be(note1.Content);

        // 5. Update Note
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes/{note1.Id}", new UpdateRecruiterNoteRequest
        {
            Content = "Updated: Candidate is highly proficient in distributed locking mechanisms."
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedNote = (await updateRes.Content.ReadFromJsonAsync<ApiResponse<RecruiterNoteResponse>>(JsonOptions))!.Data!;
        updatedNote.Content.Should().Be("Updated: Candidate is highly proficient in distributed locking mechanisms.");
        updatedNote.UpdatedAt.Should().NotBeNull();

        // 6. Delete Note
        var deleteRes = await _client.DeleteAsync($"/api/v1/recruiter/applications/{application.Id}/notes/{note1.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Verify Deleted Note returns 404
        var getDeletedRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/notes/{note1.Id}");
        getDeletedRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNote_WithInvalidContent_ReturnsBadRequest()
    {
        var (recruiterAuth, _, application) = await SetupApplicationAsync("Notes Validation Corp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        // Empty content
        var emptyRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = ""
        });
        emptyRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Whitespace-only content
        var whitespaceRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "    \r\n   "
        });
        whitespaceRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Exceeding 5000 chars
        var longRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = new string('A', 5001)
        });
        longRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrossTenant_RecruiterBCannotAccessOrModify_RecruiterANotes()
    {
        var (recruiterAAuth, _, applicationA) = await SetupApplicationAsync("Recruiter Note Tenant A");
        AuthTestHelper.SetBearerToken(_client, recruiterAAuth.AccessToken);

        // Recruiter A creates note
        var createNoteRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "Confidential note by Recruiter A."
        });
        var noteA = (await createNoteRes.Content.ReadFromJsonAsync<ApiResponse<RecruiterNoteResponse>>(JsonOptions))!.Data!;

        // Recruiter B logs in
        var (recruiterBAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "Recruiter Note Tenant B");
        AuthTestHelper.SetBearerToken(_client, recruiterBAuth.AccessToken);

        // 1. Recruiter B tries to create note on Application A -> 403 Forbidden
        var createB = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "Unauthorized note attempt."
        });
        createB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Recruiter B tries to list notes on Application A -> 403 Forbidden
        var listB = await _client.GetAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes");
        listB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Recruiter B tries to get note A -> 403 Forbidden
        var getB = await _client.GetAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes/{noteA.Id}");
        getB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Recruiter B tries to update note A -> 403 Forbidden
        var putB = await _client.PutAsJsonAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes/{noteA.Id}", new UpdateRecruiterNoteRequest
        {
            Content = "Unauthorized update attempt."
        });
        putB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Recruiter B tries to delete note A -> 403 Forbidden
        var delB = await _client.DeleteAsync($"/api/v1/recruiter/applications/{applicationA.Id}/notes/{noteA.Id}");
        delB.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JobSeeker_CannotAccessRecruiterNotes()
    {
        var (_, _, application) = await SetupApplicationAsync("JobSeeker Isolation Corp");

        var seekerAuth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, seekerAuth.AccessToken);

        var listRes = await _client.GetAsync($"/api/v1/recruiter/applications/{application.Id}/notes");
        listRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createRes = await _client.PostAsJsonAsync($"/api/v1/recruiter/applications/{application.Id}/notes", new CreateRecruiterNoteRequest
        {
            Content = "Seeker attempting note creation"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnapprovedRecruiter_CannotAccessRecruiterNotes()
    {
        var unapprovedAuth = await AuthTestHelper.CreateUnapprovedRecruiterAsync(_client, "PendingNotesCorp");
        AuthTestHelper.SetBearerToken(_client, unapprovedAuth.AccessToken);

        var listRes = await _client.GetAsync($"/api/v1/recruiter/applications/{Guid.NewGuid()}/notes");
        listRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessRecruiterNotes()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var listRes = await _client.GetAsync($"/api/v1/recruiter/applications/{Guid.NewGuid()}/notes");
        listRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
