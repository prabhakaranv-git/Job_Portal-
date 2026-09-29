using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class CandidateFeedbackConfiguration : IEntityTypeConfiguration<CandidateFeedback>
{
    public void Configure(EntityTypeBuilder<CandidateFeedback> builder)
    {
        builder.ToTable("CandidateFeedbacks");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.ApplicationId)
            .IsRequired();

        builder.Property(f => f.RecruiterId)
            .IsRequired();

        builder.Property(f => f.OverallRating)
            .IsRequired();

        builder.Property(f => f.Recommendation)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.Strengths)
            .HasMaxLength(3000);

        builder.Property(f => f.Weaknesses)
            .HasMaxLength(3000);

        builder.Property(f => f.DetailedFeedback)
            .HasMaxLength(5000);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt);

        builder.HasIndex(f => new { f.ApplicationId, f.RecruiterId })
            .IsUnique();

        builder.HasIndex(f => f.ApplicationId);
        builder.HasIndex(f => f.RecruiterId);

        builder.HasOne(f => f.Application)
            .WithMany(a => a.Feedbacks)
            .HasForeignKey(f => f.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Recruiter)
            .WithMany(r => r.Feedbacks)
            .HasForeignKey(f => f.RecruiterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
