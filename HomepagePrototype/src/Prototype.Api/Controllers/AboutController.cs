using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.About;
using Prototype.Application.UseCases.About;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/about")]
public class AboutController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AboutDto>> GetAbout(
        [FromServices] IGetAboutUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize] // nsq_auth SSO 인증 필요
    public async Task<ActionResult<AboutDto>> UpdateAbout(
        [FromBody] UpdateAboutDto dto,
        [FromServices] IUpdateAboutUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
