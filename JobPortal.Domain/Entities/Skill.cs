using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class Skill : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
    public ICollection<JobSeekerSkill> JobSeekerSkills { get; set; } = new List<JobSeekerSkill>();
}
