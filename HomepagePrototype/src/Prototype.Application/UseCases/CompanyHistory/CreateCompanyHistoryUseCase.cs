using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.CompanyHistory;

namespace Prototype.Application.UseCases.CompanyHistory;

public interface ICreateCompanyHistoryUseCase
{
    Task<CompanyHistoryDto> ExecuteAsync(CreateCompanyHistoryDto dto, CancellationToken cancellationToken = default);
}

public class CreateCompanyHistoryUseCase : ICreateCompanyHistoryUseCase
{
    private readonly IApplicationDbContext _context;

    public CreateCompanyHistoryUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyHistoryDto> ExecuteAsync(CreateCompanyHistoryDto dto, CancellationToken cancellationToken = default)
    {
        var history = new Domain.Entities.CompanyHistory(dto.Date, dto.Content);
        _context.CompanyHistories.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        return new CompanyHistoryDto(history.Id, history.Date, history.Content, history.CreatedAt);
    }
}
