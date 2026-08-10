using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.History;

namespace Prototype.Application.UseCases.History;

public interface IGetCompanyHistoriesUseCase
{
    Task<List<CompanyHistoryDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetCompanyHistoriesUseCase : IGetCompanyHistoriesUseCase
{
    private readonly IApplicationDbContext _context;

    public GetCompanyHistoriesUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CompanyHistoryDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CompanyHistories
            .OrderByDescending(h => h.Date)
            .Select(h => new CompanyHistoryDto(h.Id, h.Date, h.Content, h.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
