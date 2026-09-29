using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(j => j.Title);
        builder.HasIndex(j => j.Location);
        builder.HasIndex(j => j.JobType);
        builder.HasIndex(j => j.ExpiresAt);
        builder.HasIndex(j => j.CreatedAt);

        builder.Property(j => j.Description)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(j => j.Location)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(j => j.Status);

        // Composite indexes for query patterns:
        // 1. Status + CreatedAt: Sorting and filtering active jobs by creation date
        builder.HasIndex(j => new { j.Status, j.CreatedAt });

        // 2. Status + ExpiresAt: Filtering active, non-expired published jobs in public search
        builder.HasIndex(j => new { j.Status, j.ExpiresAt });

        // 3. RecruiterId + Status: Recruiter dashboard filtering owned jobs by status
        builder.HasIndex(j => new { j.RecruiterId, j.Status });

        builder.Property(j => j.SalaryMin)
            .HasColumnType("decimal(18,2)");

        builder.Property(j => j.SalaryMax)
            .HasColumnType("decimal(18,2)");

        builder.Property(j => j.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("USD");

        builder.Property(j => j.ExpiresAt);

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.UpdatedAt);

        builder.HasMany(j => j.Applications)
            .WithOne(a => a.Job)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
