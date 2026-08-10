using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.About;
using Prototype.Domain.Entities;

namespace Prototype.Application.UseCases.About;

public interface IUpdateAboutUseCase
{
    Task<AboutDto> ExecuteAsync(UpdateAboutDto dto, CancellationToken cancellationToken = default);
}

public class UpdateAboutUseCase : IUpdateAboutUseCase
{
    private readonly IApplicationDbContext _context;

    public UpdateAboutUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AboutDto> ExecuteAsync(UpdateAboutDto dto, CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            info = new CompanyInfo();
            info.UpdateIntroduction(dto.Introduction);
            _context.CompanyInfos.Add(info);
        }
        else
        {
            info.UpdateIntroduction(dto.Introduction);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AboutDto(info.Introduction, info.UpdatedAt);
    }
}
