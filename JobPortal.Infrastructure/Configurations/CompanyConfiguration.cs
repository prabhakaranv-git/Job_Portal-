using JobPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPortal.Infrastructure.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(c => c.Name);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.Website)
            .HasMaxLength(500);

        builder.Property(c => c.Location)
            .HasMaxLength(200);

        builder.Property(c => c.LogoUrl)
            .HasMaxLength(1000);

        builder.Property(c => c.IsVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        builder.HasMany(c => c.Recruiters)
            .WithOne(r => r.Company)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Jobs)
            .WithOne(j => j.Company)
            .HasForeignKey(j => j.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
