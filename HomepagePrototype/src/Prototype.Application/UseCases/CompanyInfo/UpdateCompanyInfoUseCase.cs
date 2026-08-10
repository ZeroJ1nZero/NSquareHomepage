using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.CompanyInfo;

namespace Prototype.Application.UseCases.CompanyInfo;

public interface IUpdateCompanyInfoUseCase
{
    Task<CompanyInfoDto> ExecuteAsync(UpdateCompanyInfoDto dto, CancellationToken cancellationToken = default);
}

public class UpdateCompanyInfoUseCase : IUpdateCompanyInfoUseCase
{
    private readonly IApplicationDbContext _context;

    public UpdateCompanyInfoUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyInfoDto> ExecuteAsync(UpdateCompanyInfoDto dto, CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            info = new Domain.Entities.CompanyInfo();
            if (dto.Introduction != null) info.UpdateIntroduction(dto.Introduction);
            if (dto.Service != null) info.UpdateService(dto.Service);
            _context.CompanyInfos.Add(info);
        }
        else
        {
            if (dto.Introduction != null) info.UpdateIntroduction(dto.Introduction);
            if (dto.Service != null) info.UpdateService(dto.Service);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new CompanyInfoDto(info.Introduction, info.Service, info.UpdatedAt);
    }
}
