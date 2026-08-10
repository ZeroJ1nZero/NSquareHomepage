using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.Service;

namespace Prototype.Application.UseCases.Service;

public interface IGetServiceUseCase
{
    Task<ServiceDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetServiceUseCase : IGetServiceUseCase
{
    private readonly IApplicationDbContext _context;

    public GetServiceUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            return new ServiceDto(string.Empty, DateTime.UtcNow);
        }

        return new ServiceDto(info.Service, info.UpdatedAt);
    }
}
