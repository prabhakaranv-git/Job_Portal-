using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsVerified { get; set; } = false;

    public ICollection<Recruiter> Recruiters { get; set; } = new List<Recruiter>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
