using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class RecruiterNote : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public Guid RecruiterId { get; set; }
    public Recruiter Recruiter { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
}
