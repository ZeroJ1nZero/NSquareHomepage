using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.DTOs.About;
using Domain.Entities;

namespace Application.UseCases.About;

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
            about = new CompanyAbout(dto.Content);
            _context.CompanyAbouts.Add(about);
        }
        else
        {
            about.Update(dto.Content);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AboutDto(about.Content, about.UpdatedAt);
    }
}
