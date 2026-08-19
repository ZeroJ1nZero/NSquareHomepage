using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.DTOs.Service;

namespace Application.UseCases.Service;

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
        var service = await _context.CompanyServices.FirstOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            return new ServiceDto(string.Empty, DateTime.UtcNow);
        }

        return new ServiceDto(service.Service, service.UpdatedAt);
    }
}
