using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Resume;

public class ResumeManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ResumeManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static MultipartFormDataContent CreateResumeForm(
        string fileName = "resume.pdf",
        string contentType = "application/pdf",
        byte[]? fileBytes = null,
        bool isDefault = false)
    {
        var form = new MultipartFormDataContent();
        var bytes = fileBytes ?? "%PDF-1.4 test resume document content"u8.ToArray();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(isDefault.ToString().ToLower()), "isDefault");
        return form;
    }

    [Fact]
    public async Task JobSeeker_UploadsPdfResume_FirstResumeBecomesDefault()
    {
        // Arrange
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var form = CreateResumeForm("john_doe_resume.pdf", "application/pdf", isDefault: false);

        // Act
        var response = await _client.PostAsync("/api/v1/jobseeker/resumes", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.FileName.Should().Be("john_doe_resume.pdf");
        result.Data.ContentType.Should().Be("application/pdf");
        result.Data.IsDefault.Should().BeTrue("First uploaded resume must become default automatically");
    }

    [Fact]
    public async Task JobSeeker_UploadsDocxResume_Succeeds()
    {
        // Arrange
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var form = CreateResumeForm("cv.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        // Act
        var response = await _client.PostAsync("/api/v1/jobseeker/resumes", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions);
        result!.Data!.FileName.Should().Be("cv.docx");
    }

    [Fact]
    public async Task JobSeeker_UploadingSecondResume_WithIsDefaultTrue_UnsetsPreviousDefault()
    {
        // Arrange
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // 1. Upload first resume (becomes default)
        var form1 = CreateResumeForm("first.pdf");
        var res1 = await _client.PostAsync("/api/v1/jobseeker/resumes", form1);
        var resume1 = (await res1.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;
        resume1.IsDefault.Should().BeTrue();

        // 2. Upload second resume marked as default
        var form2 = CreateResumeForm("second.pdf", isDefault: true);
        var res2 = await _client.PostAsync("/api/v1/jobseeker/resumes", form2);
        var resume2 = (await res2.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;
        resume2.IsDefault.Should().BeTrue();

        // 3. Verify first resume is no longer default
        var getRes1 = await _client.GetAsync($"/api/v1/jobseeker/resumes/{resume1.Id}");
        var updatedResume1 = (await getRes1.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;
        updatedResume1.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task JobSeeker_UploadsInvalidExtension_Returns400BadRequest()
    {
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var form = CreateResumeForm("script.exe", "application/octet-stream");

        var response = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JobSeeker_UploadsOversizedResume_Returns400BadRequest()
    {
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // 6 MB dummy buffer (limit is 5 MB)
        var oversized = new byte[6 * 1024 * 1024];
        var form = CreateResumeForm("large.pdf", "application/pdf", fileBytes: oversized);

        var response = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JobSeeker_ListsAndDownloadsOwnResume()
    {
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var expectedBytes = "%PDF-1.4 My Custom PDF Content"u8.ToArray();
        var form = CreateResumeForm("my_resume.pdf", "application/pdf", fileBytes: expectedBytes);
        var createRes = await _client.PostAsync("/api/v1/jobseeker/resumes", form);
        var created = (await createRes.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        // 1. List resumes
        var listRes = await _client.GetAsync("/api/v1/jobseeker/resumes");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await listRes.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<ResumeResponse>>>(JsonOptions))!.Data!;
        list.Should().Contain(r => r.Id == created.Id);

        // 2. Download resume
        var downloadRes = await _client.GetAsync($"/api/v1/jobseeker/resumes/{created.Id}/download");
        downloadRes.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadRes.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var downloadedBytes = await downloadRes.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(expectedBytes);
    }

    [Fact]
    public async Task JobSeeker_SetsDefaultResume_AndDeletesResume_PromotesNewest()
    {
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Upload resume 1
        var form1 = CreateResumeForm("doc1.pdf");
        var res1 = await _client.PostAsync("/api/v1/jobseeker/resumes", form1);
        var resume1 = (await res1.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        // Upload resume 2 (not default)
        var form2 = CreateResumeForm("doc2.pdf", isDefault: false);
        var res2 = await _client.PostAsync("/api/v1/jobseeker/resumes", form2);
        var resume2 = (await res2.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        // Set resume 2 as default
        var setDefaultRes = await _client.PatchAsync($"/api/v1/jobseeker/resumes/{resume2.Id}/default", null);
        setDefaultRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete default resume 2 -> resume 1 should be promoted to default
        var deleteRes = await _client.DeleteAsync($"/api/v1/jobseeker/resumes/{resume2.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkRes1 = await _client.GetAsync($"/api/v1/jobseeker/resumes/{resume1.Id}");
        var remaining = (await checkRes1.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;
        remaining.IsDefault.Should().BeTrue("Deleting default resume must promote newest remaining resume to default");
    }

    [Fact]
    public async Task JobSeekerA_CannotAccess_JobSeekerB_Resume()
    {
        // JobSeeker A uploads a resume
        var authA = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, authA.AccessToken);
        var formA = CreateResumeForm("secret_a.pdf");
        var resA = await _client.PostAsync("/api/v1/jobseeker/resumes", formA);
        var resumeA = (await resA.Content.ReadFromJsonAsync<ApiResponse<ResumeResponse>>(JsonOptions))!.Data!;

        // JobSeeker B attempts to access resume A
        var authB = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, authB.AccessToken);

        (await _client.GetAsync($"/api/v1/jobseeker/resumes/{resumeA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/v1/jobseeker/resumes/{resumeA.Id}/download")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PatchAsync($"/api/v1/jobseeker/resumes/{resumeA.Id}/default", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/v1/jobseeker/resumes/{resumeA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recruiter_AccessingJobSeekerResumeEndpoints_Returns403Forbidden()
    {
        var (recruiterAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "RecCorp");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        (await _client.GetAsync("/api/v1/jobseeker/resumes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
