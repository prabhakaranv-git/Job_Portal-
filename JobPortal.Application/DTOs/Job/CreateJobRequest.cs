using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Job;

public class CreateJobRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public JobType JobType { get; set; } = JobType.FullTime;
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpiresAt { get; set; }
    public List<Guid> SkillIds { get; set; } = new();
}
