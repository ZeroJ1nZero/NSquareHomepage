using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CompanyAbout> CompanyAbouts { get; }
    DbSet<CompanyService> CompanyServices { get; }
    DbSet<CompanyHistory> CompanyHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
