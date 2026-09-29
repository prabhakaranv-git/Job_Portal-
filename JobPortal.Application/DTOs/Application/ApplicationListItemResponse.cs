using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Application;

public class ApplicationListItemResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobLocation { get; set; } = string.Empty;
    public JobType JobType { get; set; }
    public Guid JobSeekerId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public Guid ResumeId { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public string? CoverLetter { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
