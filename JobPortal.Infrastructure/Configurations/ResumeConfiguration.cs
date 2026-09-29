using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("Resumes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.JobSeekerId)
            .IsRequired();

        builder.HasIndex(r => r.JobSeekerId);

        builder.HasIndex(r => new { r.JobSeekerId, r.IsDefault });

        builder.HasIndex(r => r.JobSeekerId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true");

        builder.Property(r => r.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.StoredFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.FileSizeBytes)
            .IsRequired();

        builder.Property(r => r.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);
    }
}
