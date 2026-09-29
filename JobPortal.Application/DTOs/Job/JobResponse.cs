using JobPortal.Application.DTOs.Company;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Job;

public class JobResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public CompanySummaryResponse Company { get; set; } = null!;
    public Guid RecruiterId { get; set; }
    public string Location { get; set; } = string.Empty;
    public JobType JobType { get; set; }
    public JobStatus Status { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpiresAt { get; set; }
    public List<SkillResponse> Skills { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
