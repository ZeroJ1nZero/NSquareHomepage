using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;
using ResourceServer.Application.DTOs.About;

namespace ResourceServer.Application.UseCases.About;

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
        var about = await _context.CompanyAbouts.FirstOrDefaultAsync(cancellationToken);
        if (about == null)
        {
            return new AboutDto(string.Empty, DateTime.UtcNow);
        }

        return new AboutDto(about.Introduction, about.UpdatedAt);
    }
}
