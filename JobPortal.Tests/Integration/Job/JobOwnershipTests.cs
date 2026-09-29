using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Job;

public class JobOwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public JobOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecruiterA_CannotModify_RecruiterB_Job()
    {
        // Arrange: Recruiter A creates a job
        var (authA, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Company Alpha");
        AuthTestHelper.SetBearerToken(_client, authA.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Lead Architect",
            Description = "Lead technical architecture for enterprise applications.",
            Location = "Remote",
            JobType = JobType.FullTime
        });
        var jobA = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Recruiter B is authenticated
        var (authB, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Company Beta");
        AuthTestHelper.SetBearerToken(_client, authB.AccessToken);

        // Act 1: Recruiter B attempts to update Recruiter A's job
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/jobs/{jobA.Id}", new UpdateJobRequest
        {
            Title = "Hijacked Title",
            Description = "Hijacked job description for testing.",
            Location = "Remote",
            JobType = JobType.FullTime
        });

        // Assert 1
        updateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 2: Recruiter B attempts to publish Recruiter A's job
        var pubRes = await _client.PutAsync($"/api/v1/jobs/{jobA.Id}/publish", null);
        pubRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 3: Recruiter B attempts to close Recruiter A's job
        var closeRes = await _client.PutAsync($"/api/v1/jobs/{jobA.Id}/close", null);
        closeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 4: Recruiter B attempts to archive Recruiter A's job
        var archiveRes = await _client.PutAsync($"/api/v1/jobs/{jobA.Id}/archive", null);
        archiveRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 5: Recruiter B attempts to view Recruiter A's job via recruiter endpoint
        var viewRes = await _client.GetAsync($"/api/v1/recruiter/jobs/{jobA.Id}");
        viewRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
