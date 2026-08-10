using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.CompanyInfo;

namespace Prototype.Application.UseCases.CompanyInfo;

public interface IGetCompanyInfoUseCase
{
    Task<CompanyInfoDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetCompanyInfoUseCase : IGetCompanyInfoUseCase
{
    private readonly IApplicationDbContext _context;

    public GetCompanyInfoUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyInfoDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        return new CompanyInfoDto(
            info?.Introduction ?? string.Empty,
            info?.Service ?? string.Empty,
            info?.UpdatedAt ?? DateTime.UtcNow);
    }
}
