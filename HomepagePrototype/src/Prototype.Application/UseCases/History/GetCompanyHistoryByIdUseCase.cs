using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.History;

namespace Prototype.Application.UseCases.History;

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
        var history = await _context.CompanyHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (history == null) return null;

        return new CompanyHistoryDto(history.Id, history.Date, history.Content, history.CreatedAt);
    }
}
