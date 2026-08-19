using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<BlockedIp> BlockedIps => Set<BlockedIp>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(e =>
        {
            e.ToTable("User");
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique(); 
        });

        builder.Entity<LoginAudit>(e =>
        {
            e.ToTable("LoginAudit");
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 최대 길이
            e.HasIndex(x => x.AttemptedAtUtc);
        });

        builder.Entity<BlockedIp>(e =>
        {
            e.ToTable("BlockedIp");
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => x.IpAddress).IsUnique();
        });
    }
}
