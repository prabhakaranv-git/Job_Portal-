using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Company;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Company;

public class CompanyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CompanyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ApprovedRecruiter_WithoutExistingCompany_CreatesCompanySuccessfully()
    {
        // Arrange
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CompanyCreateRequest
        {
            Name = "Acme Global Tech",
            Description = "A global leading technology company",
            Website = "https://acmeglobal.com",
            Location = "Bangalore, India",
            LogoUrl = "https://acmeglobal.com/logo.png"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<CompanyResponse>>(content, JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Acme Global Tech");
        result.Data.IsVerified.Should().BeFalse();
        result.Data.Website.Should().Be("https://acmeglobal.com");
    }

    [Fact]
    public async Task UnapprovedRecruiter_CreatingCompany_Returns403Forbidden()
    {
        // Arrange
        var auth = await AuthTestHelper.CreateUnapprovedRecruiterAsync(_client, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CompanyCreateRequest
        {
            Name = "Unapproved Venture",
            Description = "Description",
            Website = "https://unapproved.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JobSeeker_CreatingCompany_Returns403Forbidden()
    {
        // Arrange
        var auth = await AuthTestHelper.CreateJobSeekerAsync(_client);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CompanyCreateRequest
        {
            Name = "JobSeeker Corp",
            Website = "https://example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Recruiter_AlreadyBelongingToCompany_CreatingSecondCompany_Returns409Conflict()
    {
        // Arrange: Recruiter registered with companyName already belongs to a company
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Initial Corp");
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CompanyCreateRequest
        {
            Name = "Second Corp",
            Website = "https://secondcorp.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApprovedRecruiter_UpdatesOwnCompany_Returns200OK()
    {
        // Arrange
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var createReq = new CompanyCreateRequest
        {
            Name = "Original Name",
            Description = "Original Description",
            Website = "https://orig.com",
            Location = "Original Location"
        };
        var createRes = await _client.PostAsJsonAsync("/api/v1/companies", createReq);
        var createData = (await createRes.Content.ReadFromJsonAsync<ApiResponse<CompanyResponse>>(JsonOptions))!.Data!;

        var updateReq = new CompanyUpdateRequest
        {
            Name = "Updated Name Ltd",
            Description = "Updated Description",
            Website = "https://updated.com",
            Location = "Updated Location",
            LogoUrl = "https://updated.com/logo.png"
        };

        // Act
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/companies/{createData.Id}", updateReq);

        // Assert
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedData = (await updateRes.Content.ReadFromJsonAsync<ApiResponse<CompanyResponse>>(JsonOptions))!.Data!;
        updatedData.Name.Should().Be("Updated Name Ltd");
        updatedData.Description.Should().Be("Updated Description");
        updatedData.Website.Should().Be("https://updated.com");
    }

    [Fact]
    public async Task RecruiterA_UpdatingRecruiterBCompany_Returns403Forbidden()
    {
        // Arrange: Recruiter A creates Company A
        var (authA, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, authA.AccessToken);
        var createResA = await _client.PostAsJsonAsync("/api/v1/companies", new CompanyCreateRequest
        {
            Name = "Company A",
            Website = "https://companya.com"
        });
        var companyA = (await createResA.Content.ReadFromJsonAsync<ApiResponse<CompanyResponse>>(JsonOptions))!.Data!;

        // Recruiter B is approved with their own company B
        var (authB, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: "Company B");
        AuthTestHelper.SetBearerToken(_client, authB.AccessToken);

        var updateReq = new CompanyUpdateRequest
        {
            Name = "Malicious Hijack",
            Website = "https://hijack.com"
        };

        // Act: Recruiter B attempts to update Company A
        var response = await _client.PutAsJsonAsync($"/api/v1/companies/{companyA.Id}", updateReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCompanyById_PublicAccess_Returns200OK()
    {
        // Arrange: Create company as recruiter
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);
        var createRes = await _client.PostAsJsonAsync("/api/v1/companies", new CompanyCreateRequest
        {
            Name = "Publicly Visible Company",
            Website = "https://publiccompany.com",
            Location = "Remote"
        });
        var company = (await createRes.Content.ReadFromJsonAsync<ApiResponse<CompanyResponse>>(JsonOptions))!.Data!;

        // Act: Clear authorization header to simulate anonymous public user
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/v1/companies/{company.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CompanyResponse>>(JsonOptions);
        result!.Data!.Name.Should().Be("Publicly Visible Company");
    }

    [Fact]
    public async Task GetNonExistentCompany_Returns404NotFound()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCompany_WithInvalidName_Returns400BadRequest()
    {
        var (auth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, companyName: null);
        AuthTestHelper.SetBearerToken(_client, auth.AccessToken);

        var request = new CompanyCreateRequest
        {
            Name = "" // Invalid
        };

        var response = await _client.PostAsJsonAsync("/api/v1/companies", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
