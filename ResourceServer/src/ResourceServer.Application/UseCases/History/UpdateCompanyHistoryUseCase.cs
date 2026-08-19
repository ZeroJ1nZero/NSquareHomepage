using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;
using ResourceServer.Application.DTOs.History;

namespace ResourceServer.Application.UseCases.History;

public interface IUpdateCompanyHistoryUseCase
{
    Task<CompanyHistoryDto?> ExecuteAsync(int id, UpdateCompanyHistoryDto dto, CancellationToken cancellationToken = default);
}

public class UpdateCompanyHistoryUseCase : IUpdateCompanyHistoryUseCase
{
    private readonly IApplicationDbContext _context;

    public UpdateCompanyHistoryUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyHistoryDto?> ExecuteAsync(int id, UpdateCompanyHistoryDto dto, CancellationToken cancellationToken = default)
    {
        var history = await _context.CompanyHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (history == null)
        {
            return null;
        }

        history.Update(dto.Date, dto.Content);
        await _context.SaveChangesAsync(cancellationToken);

        return new CompanyHistoryDto(history.Id, history.Date, history.Content, history.CreatedAt);
    }
}
