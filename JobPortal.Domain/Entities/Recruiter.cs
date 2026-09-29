using JobPortal.Domain.Common;
using JobPortal.Domain.Enums;

namespace JobPortal.Domain.Entities;

public class Recruiter : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? Position { get; set; }
    public bool IsApproved { get; set; } = false;
    public RecruiterApprovalStatus ApprovalStatus { get; set; } = RecruiterApprovalStatus.Pending;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
    public ICollection<RecruiterNote> Notes { get; set; } = new List<RecruiterNote>();
    public ICollection<CandidateFeedback> Feedbacks { get; set; } = new List<CandidateFeedback>();
}
