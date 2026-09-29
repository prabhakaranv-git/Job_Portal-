using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using JobApplication = JobPortal.Domain.Entities.Application;

namespace JobPortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<JobSeeker> JobSeekers { get; }
    DbSet<Recruiter> Recruiters { get; }
    DbSet<Company> Companies { get; }
    DbSet<Job> Jobs { get; }
    DbSet<JobApplication> Applications { get; }
    DbSet<Resume> Resumes { get; }
    DbSet<Skill> Skills { get; }
    DbSet<JobSkill> JobSkills { get; }
    DbSet<JobSeekerSkill> JobSeekerSkills { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<RecruiterNote> RecruiterNotes { get; }
    DbSet<CandidateFeedback> CandidateFeedbacks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
