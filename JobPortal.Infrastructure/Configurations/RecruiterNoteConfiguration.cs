using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class RecruiterNoteConfiguration : IEntityTypeConfiguration<RecruiterNote>
{
    public void Configure(EntityTypeBuilder<RecruiterNote> builder)
    {
        builder.ToTable("RecruiterNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(n => n.ApplicationId)
            .IsRequired();

        builder.Property(n => n.RecruiterId)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.UpdatedAt);

        builder.HasIndex(n => n.ApplicationId);
        builder.HasIndex(n => n.RecruiterId);
        builder.HasIndex(n => new { n.ApplicationId, n.CreatedAt });
        builder.HasIndex(n => new { n.RecruiterId, n.CreatedAt });

        builder.HasOne(n => n.Application)
            .WithMany(a => a.Notes)
            .HasForeignKey(n => n.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Recruiter)
            .WithMany(r => r.Notes)
            .HasForeignKey(n => n.RecruiterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
