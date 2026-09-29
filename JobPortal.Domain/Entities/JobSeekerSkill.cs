using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class JobSeekerSkill : BaseEntity
{
    public Guid JobSeekerId { get; set; }
    public JobSeeker JobSeeker { get; set; } = null!;
    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public int ProficiencyLevel { get; set; } = 1; // 1 to 5 scale
}
