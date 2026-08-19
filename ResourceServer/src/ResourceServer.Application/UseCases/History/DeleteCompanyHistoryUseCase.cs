using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;

namespace ResourceServer.Application.UseCases.History;

public interface IDeleteCompanyHistoryUseCase
{
    Task<bool> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public class DeleteCompanyHistoryUseCase : IDeleteCompanyHistoryUseCase
{
    private readonly IApplicationDbContext _context;

    public DeleteCompanyHistoryUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var history = await _context.CompanyHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (history == null)
        {
            return false;
        }

        _context.CompanyHistories.Remove(history);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
