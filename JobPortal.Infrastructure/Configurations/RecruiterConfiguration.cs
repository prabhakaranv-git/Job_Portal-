using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class RecruiterConfiguration : IEntityTypeConfiguration<Recruiter>
{
    public void Configure(EntityTypeBuilder<Recruiter> builder)
    {
        builder.ToTable("Recruiters");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.HasIndex(r => r.UserId)
            .IsUnique();

        builder.Property(r => r.Position)
            .HasMaxLength(150);

        builder.Property(r => r.IsApproved)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ApprovalStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(RecruiterApprovalStatus.Pending);

        builder.HasIndex(r => r.ApprovalStatus);

        builder.Property(r => r.ApprovedAt);

        builder.Property(r => r.RejectedAt);

        builder.Property(r => r.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);

        builder.HasMany(r => r.Jobs)
            .WithOne(j => j.Recruiter)
            .HasForeignKey(j => j.RecruiterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
