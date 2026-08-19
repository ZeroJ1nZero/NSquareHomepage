using Microsoft.EntityFrameworkCore;
using ResourceServer.Application.Common.Interfaces;
using ResourceServer.Application.DTOs.About;
using ResourceServer.Domain.Entities;

namespace ResourceServer.Application.UseCases.About;

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
        var about = await _context.CompanyAbouts.FirstOrDefaultAsync(cancellationToken);
        if (about == null)
        {
            about = new CompanyAbout(dto.Introduction);
            _context.CompanyAbouts.Add(about);
        }
        else
        {
            about.Update(dto.Introduction);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AboutDto(about.Introduction, about.UpdatedAt);
    }
}
