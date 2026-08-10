using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.Service;
using Prototype.Domain.Entities;

namespace Prototype.Application.UseCases.Service;

public interface IUpdateServiceUseCase
{
    Task<ServiceDto> ExecuteAsync(UpdateServiceDto dto, CancellationToken cancellationToken = default);
}

public class UpdateServiceUseCase : IUpdateServiceUseCase
{
    private readonly IApplicationDbContext _context;

    public UpdateServiceUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceDto> ExecuteAsync(UpdateServiceDto dto, CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            info = new CompanyInfo();
            info.UpdateService(dto.Service);
            _context.CompanyInfos.Add(info);
        }
        else
        {
            info.UpdateService(dto.Service);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ServiceDto(info.Service, info.UpdatedAt);
    }
}
