using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.DTOs.About;

namespace Application.UseCases.About;

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

        return new AboutDto(about.Content, about.UpdatedAt);
    }
}
