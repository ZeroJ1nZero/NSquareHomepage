using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities;

namespace Infrastructure.Persistence.Configurations;

public class CompanyHistoryConfiguration : IEntityTypeConfiguration<CompanyHistory>
{
    public void Configure(EntityTypeBuilder<CompanyHistory> builder)
    {
        builder.ToTable("CompanyHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.EventDate)
            .IsRequired();

        builder.Property(h => h.Content)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .IsRequired();
    }
}
