using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class JobSeeker : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string? Headline { get; set; }
    public string? Summary { get; set; }
    public int ExperienceYears { get; set; }
    public string? CurrentLocation { get; set; }

    public ICollection<Resume> Resumes { get; set; } = new List<Resume>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<JobSeekerSkill> Skills { get; set; } = new List<JobSeekerSkill>();
}
