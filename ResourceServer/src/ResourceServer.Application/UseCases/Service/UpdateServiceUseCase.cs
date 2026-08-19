using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;
using ResourceServer.Application.DTOs.Service;
using ResourceServer.Domain.Entities;

namespace ResourceServer.Application.UseCases.Service;

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
        var service = await _context.CompanyServices.FirstOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            service = new CompanyService(dto.Service);
            _context.CompanyServices.Add(service);
        }
        else
        {
            service.Update(dto.Service);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ServiceDto(service.Service, service.UpdatedAt);
    }
}
