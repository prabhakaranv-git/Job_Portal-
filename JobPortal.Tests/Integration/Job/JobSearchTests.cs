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

public class JobSearchTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public JobSearchTests(CustomWebApplicationFactory factory)
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

    private async Task<JobResponse> CreateAndPublishJobAsync(
        string companyName,
        string title,
        string description,
        string location,
        JobType jobType,
        decimal? salaryMin,
        decimal? salaryMax,
        DateTime? expiresAt = null,
        List<Guid>? skillIds = null)
    {
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: companyName);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = title,
            Description = description,
            Location = location,
            JobType = jobType,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            Currency = "USD",
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            SkillIds = skillIds ?? new List<Guid>()
        });

        var job = (await createRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{job.Id}/publish", null);
        return job;
    }

    [Fact]
    public async Task SearchJobs_OnlyReturnsActivePublishedJobs()
    {
        var uniqueTag = Guid.NewGuid().ToString("N");
        var activeJob = await CreateAndPublishJobAsync($"Company_{uniqueTag}", $"Dev_{uniqueTag}", "Active description test", "Berlin", JobType.FullTime, 50000, 70000);

        // Create a Draft job that is never published
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: $"DraftCo_{uniqueTag}");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);
        var draftRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = $"Draft_{uniqueTag}",
            Description = "Draft job description",
            Location = "Berlin",
            JobType = JobType.FullTime
        });
        var draftJob = (await draftRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        // Act: Public search by unique tag
        _client.DefaultRequestHeaders.Authorization = null;
        var searchRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}");

        // Assert
        searchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await searchRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        result.Items.Should().Contain(j => j.Id == activeJob.Id);
        result.Items.Should().NotContain(j => j.Id == draftJob.Id);

        // Public get by ID on draft job returns 404
        var draftGetRes = await _client.GetAsync($"/api/v1/jobs/{draftJob.Id}");
        draftGetRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchJobs_ExcludesExpiredJobs()
    {
        var uniqueTag = Guid.NewGuid().ToString("N");
        var activeJob = await CreateAndPublishJobAsync($"ActiveCo_{uniqueTag}", $"ActiveJob_{uniqueTag}", "Description test", "London", JobType.FullTime, 60000, 90000);

        // Create a job and manually expire it in DB
        var expiredJob = await CreateAndPublishJobAsync($"ExpiredCo_{uniqueTag}", $"ExpiredJob_{uniqueTag}", "Description test", "London", JobType.FullTime, 60000, 90000);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPortal.Infrastructure.Data.ApplicationDbContext>();
            var dbJob = db.Jobs.First(j => j.Id == expiredJob.Id);
            dbJob.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        // Act: Public search
        _client.DefaultRequestHeaders.Authorization = null;
        var searchRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}");

        // Assert
        searchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await searchRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        result.Items.Should().Contain(j => j.Id == activeJob.Id);
        result.Items.Should().NotContain(j => j.Id == expiredJob.Id);

        // Public get by ID on expired job returns 404
        var expiredGetRes = await _client.GetAsync($"/api/v1/jobs/{expiredJob.Id}");
        expiredGetRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchJobs_FiltersByLocationAndJobType()
    {
        var uniqueTag = Guid.NewGuid().ToString("N");
        var jobTokyo = await CreateAndPublishJobAsync($"TokyoCo_{uniqueTag}", $"TokyoDev_{uniqueTag}", "Tokyo developer role", "Tokyo, Japan", JobType.FullTime, 70000, 100000);
        var jobRemote = await CreateAndPublishJobAsync($"RemoteCo_{uniqueTag}", $"RemoteDev_{uniqueTag}", "Remote developer role", "Remote", JobType.Remote, 70000, 100000);

        _client.DefaultRequestHeaders.Authorization = null;

        // Filter by Location
        var locRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}&location=Tokyo");
        locRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var locData = (await locRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        locData.Items.Should().Contain(j => j.Id == jobTokyo.Id);
        locData.Items.Should().NotContain(j => j.Id == jobRemote.Id);

        // Filter by JobType
        var typeRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}&jobType=Remote");
        typeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var typeData = (await typeRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        typeData.Items.Should().Contain(j => j.Id == jobRemote.Id);
        typeData.Items.Should().NotContain(j => j.Id == jobTokyo.Id);
    }

    [Fact]
    public async Task SearchJobs_FiltersBySalaryRange()
    {
        var uniqueTag = Guid.NewGuid().ToString("N");
        // Job 1: 30k to 50k
        var jobLow = await CreateAndPublishJobAsync($"LowCo_{uniqueTag}", $"LowSalary_{uniqueTag}", "Junior developer role", "Austin", JobType.FullTime, 30000, 50000);
        // Job 2: 80k to 120k
        var jobHigh = await CreateAndPublishJobAsync($"HighCo_{uniqueTag}", $"HighSalary_{uniqueTag}", "Principal developer role", "Austin", JobType.FullTime, 80000, 120000);

        _client.DefaultRequestHeaders.Authorization = null;

        // Filter: salaryMin = 70000 (job.SalaryMax >= 70000)
        var minRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}&salaryMin=70000");
        var minData = (await minRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        minData.Items.Should().Contain(j => j.Id == jobHigh.Id);
        minData.Items.Should().NotContain(j => j.Id == jobLow.Id);

        // Filter: salaryMax = 60000 (job.SalaryMin <= 60000)
        var maxRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}&salaryMax=60000");
        var maxData = (await maxRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        maxData.Items.Should().Contain(j => j.Id == jobLow.Id);
        maxData.Items.Should().NotContain(j => j.Id == jobHigh.Id);
    }

    [Fact]
    public async Task SearchJobs_FiltersBySkills()
    {
        var skillRust = await CreateSkillAsync($"Rust_{Guid.NewGuid():N}");
        var skillKotlin = await CreateSkillAsync($"Kotlin_{Guid.NewGuid():N}");

        var uniqueTag = Guid.NewGuid().ToString("N");
        var rustJob = await CreateAndPublishJobAsync($"RustCo_{uniqueTag}", $"RustDev_{uniqueTag}", "Rust systems programmer", "Seattle", JobType.FullTime, 90000, 140000, skillIds: new List<Guid> { skillRust });
        var kotlinJob = await CreateAndPublishJobAsync($"KotlinCo_{uniqueTag}", $"KotlinDev_{uniqueTag}", "Android Kotlin programmer", "Seattle", JobType.FullTime, 90000, 140000, skillIds: new List<Guid> { skillKotlin });

        _client.DefaultRequestHeaders.Authorization = null;

        // Search filtering by skillRust ID
        var skillRes = await _client.GetAsync($"/api/v1/jobs?search={uniqueTag}&skillIds={skillRust}");
        var skillData = (await skillRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        skillData.Items.Should().Contain(j => j.Id == rustJob.Id);
        skillData.Items.Should().NotContain(j => j.Id == kotlinJob.Id);
    }

    [Fact]
    public async Task SearchJobs_PaginationAndSorting()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var pagedRes = await _client.GetAsync("/api/v1/jobs?page=1&pageSize=5&sortBy=createdat&sortDirection=desc");
        pagedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedData = (await pagedRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        pagedData.Page.Should().Be(1);
        pagedData.PageSize.Should().Be(5);
        pagedData.Items.Count.Should().BeLessOrEqualTo(5);

        // Invalid page < 1 returns 400
        var invalidPageRes = await _client.GetAsync("/api/v1/jobs?page=0");
        invalidPageRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // PageSize > 50 returns 400
        var invalidPageSizeRes = await _client.GetAsync("/api/v1/jobs?pageSize=100");
        invalidPageSizeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecruiterJobsEndpoint_ReturnsRecruiterOwnedJobsAcrossStatuses()
    {
        var (auth, recruiterId) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "MultiStatus Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        // Create 1 Draft, 1 Published
        var draftRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Draft Job Recruiter Only",
            Description = "Should be visible only to the owning recruiter.",
            Location = "Remote",
            JobType = JobType.PartTime
        });
        var draftJob = (await draftRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;

        var pubRes = await _client.PostAsJsonAsync("/api/v1/jobs", new CreateJobRequest
        {
            Title = "Published Job Recruiter",
            Description = "Should be visible publicly and to recruiter.",
            Location = "Remote",
            JobType = JobType.PartTime
        });
        var pubJob = (await pubRes.Content.ReadFromJsonAsync<ApiResponse<JobResponse>>(JsonOptions))!.Data!;
        await _client.PutAsync($"/api/v1/jobs/{pubJob.Id}/publish", null);

        // Act: Call recruiter jobs endpoint
        var recRes = await _client.GetAsync("/api/v1/recruiter/jobs");

        // Assert
        recRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var recData = (await recRes.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<JobListItemResponse>>>(JsonOptions))!.Data!;
        recData.Items.Should().Contain(j => j.Id == draftJob.Id);
        recData.Items.Should().Contain(j => j.Id == pubJob.Id);

        // Recruiter detail endpoint for draft job
        var draftDetailRes = await _client.GetAsync($"/api/v1/recruiter/jobs/{draftJob.Id}");
        draftDetailRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
