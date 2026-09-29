using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> builder)
    {
        builder.ToTable("JobSkills");

        builder.HasKey(js => js.Id);

        builder.HasIndex(js => new { js.JobId, js.SkillId })
            .IsUnique();

        builder.Property(js => js.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(js => js.CreatedAt)
            .IsRequired();

        builder.Property(js => js.UpdatedAt);

        builder.HasOne(js => js.Job)
            .WithMany(j => j.Skills)
            .HasForeignKey(js => js.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(js => js.Skill)
            .WithMany(s => s.JobSkills)
            .HasForeignKey(js => js.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
