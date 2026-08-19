using Microsoft.EntityFrameworkCore;
using ResourceServer.Domain.Entities;

namespace ResourceServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CompanyAbout> CompanyAbouts { get; }
    DbSet<CompanyService> CompanyServices { get; }
    DbSet<CompanyHistory> CompanyHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
