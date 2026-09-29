using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class JobSeekerSkillConfiguration : IEntityTypeConfiguration<JobSeekerSkill>
{
    public void Configure(EntityTypeBuilder<JobSeekerSkill> builder)
    {
        builder.ToTable("JobSeekerSkills");

        builder.HasKey(jss => jss.Id);

        builder.HasIndex(jss => new { jss.JobSeekerId, jss.SkillId })
            .IsUnique();

        builder.Property(jss => jss.ProficiencyLevel)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(jss => jss.CreatedAt)
            .IsRequired();

        builder.Property(jss => jss.UpdatedAt);

        builder.HasOne(jss => jss.JobSeeker)
            .WithMany(js => js.Skills)
            .HasForeignKey(jss => jss.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(jss => jss.Skill)
            .WithMany(s => s.JobSeekerSkills)
            .HasForeignKey(jss => jss.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
