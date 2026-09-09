using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.DTOs.History;

namespace Application.UseCases.History;

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
            .OrderByDescending(h => h.EventDate)
            .Select(h => new CompanyHistoryDto(h.Id, h.EventDate, h.Content, h.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
