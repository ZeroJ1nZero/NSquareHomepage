using Microsoft.EntityFrameworkCore;
using Prototype.Domain.Entities;

namespace Prototype.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CompanyInfo> CompanyInfos { get; }
    DbSet<CompanyHistory> CompanyHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
