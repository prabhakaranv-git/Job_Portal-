namespace JobPortal.Application.DTOs.Admin;

public class RecruiterApprovalResponseDto
{
    public Guid RecruiterId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Position { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RejectRecruiterRequestDto
{
    public string? Reason { get; set; }
}
