using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;
using ResourceServer.Domain.Entities;

namespace ResourceServer.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CompanyAbout> CompanyAbouts => Set<CompanyAbout>();
    public DbSet<CompanyService> CompanyServices => Set<CompanyService>();
    public DbSet<CompanyHistory> CompanyHistories => Set<CompanyHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
