using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Application;

public class ApplicationResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid JobSeekerId { get; set; }
    public Guid ResumeId { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public string? CoverLetter { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
