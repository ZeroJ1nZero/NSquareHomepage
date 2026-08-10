using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prototype.Domain.Entities;

namespace Prototype.Infrastructure.Persistence.Configurations;

public class CompanyInfoConfiguration : IEntityTypeConfiguration<CompanyInfo>
{
    public void Configure(EntityTypeBuilder<CompanyInfo> builder)
    {
        builder.ToTable("CompanyInfos");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Introduction)
            .HasMaxLength(4000);

        builder.Property(c => c.Service)
            .HasMaxLength(4000);

        builder.Property(c => c.UpdatedAt)
            .IsRequired();
    }
}
