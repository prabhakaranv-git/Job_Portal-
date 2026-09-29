using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class JobSeekerConfiguration : IEntityTypeConfiguration<JobSeeker>
{
    public void Configure(EntityTypeBuilder<JobSeeker> builder)
    {
        builder.ToTable("JobSeekers");

        builder.HasKey(js => js.Id);

        builder.Property(js => js.UserId)
            .IsRequired();

        builder.HasIndex(js => js.UserId)
            .IsUnique();

        builder.Property(js => js.Headline)
            .HasMaxLength(200);

        builder.Property(js => js.Summary)
            .HasMaxLength(3000);

        builder.Property(js => js.ExperienceYears)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(js => js.CurrentLocation)
            .HasMaxLength(200);

        builder.Property(js => js.CreatedAt)
            .IsRequired();

        builder.Property(js => js.UpdatedAt);

        builder.HasMany(js => js.Resumes)
            .WithOne(r => r.JobSeeker)
            .HasForeignKey(r => r.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(js => js.Applications)
            .WithOne(a => a.JobSeeker)
            .HasForeignKey(a => a.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
