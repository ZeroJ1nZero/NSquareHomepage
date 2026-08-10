using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Domain.Entities;

namespace Prototype.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();
    public DbSet<CompanyHistory> CompanyHistories => Set<CompanyHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
