using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourceServer.Domain.Entities;

namespace ResourceServer.Infrastructure.Persistence.Configurations;

public class CompanyAboutConfiguration : IEntityTypeConfiguration<CompanyAbout>
{
    public void Configure(EntityTypeBuilder<CompanyAbout> builder)
    {
        builder.ToTable("CompanyAbouts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Introduction)
            .HasMaxLength(4000);

        builder.Property(c => c.UpdatedAt)
            .IsRequired();
    }
}
