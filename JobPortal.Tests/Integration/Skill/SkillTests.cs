using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Domain.Enums;
using JobPortal.Tests.Infrastructure;
using Xunit;

namespace JobPortal.Tests.Integration.Skill;

public class SkillTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SkillTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_CreatesSkill_Returns201Created()
    {
        // Arrange
        var adminAuth = await AuthTestHelper.CreateAdminAsync(_client, _factory);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);

        var request = new CreateSkillRequest
        {
            Name = $"C# .NET Core {Guid.NewGuid():N}",
            Description = "Backend development with .NET"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/skills", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SkillResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task NonAdmin_CreatingSkill_Returns403Forbidden()
    {
        // Arrange: Recruiter attempts to create skill
        var (recruiterAuth, _) = await AuthTestHelper.CreateApprovedRecruiterAsync(_client, _factory, "SomeCompany");
        AuthTestHelper.SetBearerToken(_client, recruiterAuth.AccessToken);

        var request = new CreateSkillRequest
        {
            Name = "Python",
            Description = "Programming language"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/skills", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_CreatingSkill_Returns401Unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var request = new CreateSkillRequest
        {
            Name = "Golang",
            Description = "Google Go"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/skills", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_CreatingDuplicateSkill_Returns409Conflict()
    {
        // Arrange
        var adminAuth = await AuthTestHelper.CreateAdminAsync(_client, _factory);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);

        var uniqueSkill = $"PostgreSQL {Guid.NewGuid():N}";
        var firstReq = new CreateSkillRequest { Name = uniqueSkill, Description = "Relational DB" };
        var firstRes = await _client.PostAsJsonAsync("/api/v1/skills", firstReq);
        firstRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Attempt to create skill with same name (different casing / leading whitespace)
        var dupReq = new CreateSkillRequest { Name = $"  {uniqueSkill.ToLower()}  ", Description = "Same DB" };
        var dupRes = await _client.PostAsJsonAsync("/api/v1/skills", dupReq);

        // Assert
        dupRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetAllSkills_PublicAccess_Returns200OK()
    {
        // Arrange: Create a skill as admin
        var adminAuth = await AuthTestHelper.CreateAdminAsync(_client, _factory);
        AuthTestHelper.SetBearerToken(_client, adminAuth.AccessToken);
        await _client.PostAsJsonAsync("/api/v1/skills", new CreateSkillRequest { Name = $"Skill_{Guid.NewGuid():N}" });

        // Act: Anonymous user gets skills
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/v1/skills");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<SkillResponse>>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeEmpty();
    }
}
