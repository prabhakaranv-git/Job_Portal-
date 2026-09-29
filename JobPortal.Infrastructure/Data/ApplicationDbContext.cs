using JobPortal.Application.Common.Interfaces;
using JobPortal.Domain.Common;
using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using JobApplication = JobPortal.Domain.Entities.Application;

namespace JobPortal.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTimeProvider dateTimeProvider)
        : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<JobSeeker> JobSeekers => Set<JobSeeker>();
    public DbSet<Recruiter> Recruiters => Set<Recruiter>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<JobSeekerSkill> JobSeekerSkills => Set<JobSeekerSkill>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RecruiterNote> RecruiterNotes => Set<RecruiterNote>();
    public DbSet<CandidateFeedback> CandidateFeedbacks => Set<CandidateFeedback>();

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        return await Database.CanConnectAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = utcNow;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
