using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace JobPortal.Tests.Infrastructure;

public static class AuthTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<AuthResponseDto> RegisterUserAsync(
        HttpClient client,
        string email,
        string password,
        UserRole role,
        string firstName = "Test",
        string lastName = "User",
        string? companyName = null)
    {
        var request = new RegisterRequestDto
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            CompanyName = companyName
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);
        return apiResponse!.Data!;
    }

    public static async Task<AuthResponseDto> LoginUserAsync(HttpClient client, string email, string password)
    {
        var request = new LoginRequestDto
        {
            Email = email,
            Password = password
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(content, JsonOptions);
        return apiResponse!.Data!;
    }

    public static void SetBearerToken(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static async Task<User> SeedAdminUserAsync(CustomWebApplicationFactory factory, string email = "admin@jobportal.local", string password = "AdminPassword123!")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = db.Users.FirstOrDefault(u => u.Email == email.ToLower());
        if (existing != null)
            return existing;

        var hasher = new JobPortal.Infrastructure.Services.BcryptPasswordHasher();
        var admin = new User
        {
            Email = email.ToLower(),
            PasswordHash = hasher.HashPassword(password),
            FirstName = "System",
            LastName = "Admin",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    public static async Task<(AuthResponseDto Auth, Guid RecruiterId)> CreateApprovedRecruiterAsync(
        HttpClient client,
        CustomWebApplicationFactory factory,
        string? companyName = null)
    {
        var email = $"recruiter_{Guid.NewGuid():N}@test.com";
        var auth = await RegisterUserAsync(client, email, "Password123!", UserRole.Recruiter, companyName: companyName);

        Guid recruiterId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recruiter = db.Recruiters.First(r => r.UserId == auth.UserId);
            recruiter.IsApproved = true;
            recruiter.ApprovalStatus = RecruiterApprovalStatus.Approved;
            await db.SaveChangesAsync();
            recruiterId = recruiter.Id;
        }

        return (auth, recruiterId);
    }

    public static async Task<AuthResponseDto> CreateUnapprovedRecruiterAsync(
        HttpClient client,
        string? companyName = null)
    {
        var email = $"unapproved_{Guid.NewGuid():N}@test.com";
        return await RegisterUserAsync(client, email, "Password123!", UserRole.Recruiter, companyName: companyName);
    }

    public static async Task<AuthResponseDto> CreateJobSeekerAsync(HttpClient client)
    {
        var email = $"seeker_{Guid.NewGuid():N}@test.com";
        return await RegisterUserAsync(client, email, "Password123!", UserRole.JobSeeker);
    }

    public static async Task<AuthResponseDto> CreateAdminAsync(HttpClient client, CustomWebApplicationFactory factory)
    {
        var email = $"admin_{Guid.NewGuid():N}@jobportal.local";
        var password = "AdminPassword123!";
        await SeedAdminUserAsync(factory, email, password);
        return await LoginUserAsync(client, email, password);
    }
}
