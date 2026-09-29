using JobPortal.Domain.Common;
using JobPortal.Domain.Enums;

namespace JobPortal.Domain.Entities;

public class Job : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid RecruiterId { get; set; }
    public Recruiter Recruiter { get; set; } = null!;
    public string Location { get; set; } = string.Empty;
    public JobType JobType { get; set; } = JobType.FullTime;
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpiresAt { get; set; }

    public ICollection<JobSkill> Skills { get; set; } = new List<JobSkill>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
}
