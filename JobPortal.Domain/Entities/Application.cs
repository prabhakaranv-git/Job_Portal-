using JobPortal.Domain.Common;
using JobPortal.Domain.Enums;

namespace JobPortal.Domain.Entities;

public class Application : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid JobSeekerId { get; set; }
    public JobSeeker JobSeeker { get; set; } = null!;
    public Guid ResumeId { get; set; }
    public Resume Resume { get; set; } = null!;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public string? CoverLetter { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RecruiterNote> Notes { get; set; } = new List<RecruiterNote>();
    public ICollection<CandidateFeedback> Feedbacks { get; set; } = new List<CandidateFeedback>();
}
