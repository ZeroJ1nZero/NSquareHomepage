using Application.Interfaces;
using Application.DTOs.History;
using Domain.Entities;

namespace Application.UseCases.History;

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
        var history = new CompanyHistory(dto.Date, dto.Content);
        _context.CompanyHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return new CompanyHistoryDto(history.Id, history.Date, history.Content, history.CreatedAt);
    }
}
