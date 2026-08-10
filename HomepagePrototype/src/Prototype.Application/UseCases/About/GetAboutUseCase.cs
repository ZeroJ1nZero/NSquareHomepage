using Microsoft.EntityFrameworkCore;
using Prototype.Application.Common.Interfaces;
using Prototype.Application.DTOs.About;

namespace Prototype.Application.UseCases.About;

public interface IGetAboutUseCase
{
    Task<AboutDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetAboutUseCase : IGetAboutUseCase
{
    private readonly IApplicationDbContext _context;

    public GetAboutUseCase(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AboutDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var info = await _context.CompanyInfos.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            return new AboutDto(string.Empty, DateTime.UtcNow);
        }

        return new AboutDto(info.Introduction, info.UpdatedAt);
    }
}
