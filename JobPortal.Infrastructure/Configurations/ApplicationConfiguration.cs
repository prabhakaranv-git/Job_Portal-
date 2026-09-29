using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JobApplication = JobPortal.Domain.Entities.Application;

namespace JobPortal.Infrastructure.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.JobId)
            .IsRequired();

        builder.Property(a => a.JobSeekerId)
            .IsRequired();

        builder.HasIndex(a => new { a.JobId, a.JobSeekerId })
            .IsUnique();

        builder.HasIndex(a => a.JobId);

        builder.HasIndex(a => a.JobSeekerId);

        builder.HasIndex(a => a.AppliedAt);

        builder.HasIndex(a => new { a.JobId, a.Status });

        builder.HasIndex(a => new { a.JobSeekerId, a.AppliedAt });

        builder.Property(a => a.ResumeId)
            .IsRequired();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(a => a.Status);

        builder.Property(a => a.CoverLetter)
            .HasMaxLength(5000);

        builder.Property(a => a.AppliedAt)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.JobSeeker)
            .WithMany(js => js.Applications)
            .HasForeignKey(a => a.JobSeekerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Resume)
            .WithMany(r => r.Applications)
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
