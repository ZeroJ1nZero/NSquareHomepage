using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.CompanyHistory;

namespace Prototype.Application.UseCases.CompanyHistory;

public interface IGetCompanyHistoryByIdUseCase
{
    Task<CompanyHistoryDto?> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public class GetCompanyHistoryByIdUseCase : IGetCompanyHistoryByIdUseCase
{
    private readonly IApplicationDbContext _context;

    public GetCompanyHistoryByIdUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyHistoryDto?> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var h = await _context.CompanyHistories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (h == null) return null;

        return new CompanyHistoryDto(h.Id, h.Date, h.Content, h.CreatedAt);
    }
}
