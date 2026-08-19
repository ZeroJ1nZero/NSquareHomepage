using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities;

namespace Infrastructure.Persistence.Configurations;

public class CompanyServiceConfiguration : IEntityTypeConfiguration<CompanyService>
{
    public void Configure(EntityTypeBuilder<CompanyService> builder)
    {
        builder.ToTable("CompanyServices");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Service)
            .HasMaxLength(4000);

        builder.Property(c => c.UpdatedAt)
            .IsRequired();
    }
}
