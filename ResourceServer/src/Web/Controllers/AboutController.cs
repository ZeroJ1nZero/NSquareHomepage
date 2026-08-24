using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.About;
using Application.UseCases.About;

namespace Web.Controllers;

[ApiController]
[Route("api/Home/about")]
[Tags("1. [파이프라인 Step 10] Zero-Trust 리소스 CRUD (JWT Bearer 검증)")]
public class AboutController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<AboutDto>> GetAbout(
        [FromServices] IGetAboutUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AboutDto>> UpdateAbout(
        [FromBody] UpdateAboutDto dto,
        [FromServices] IUpdateAboutUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
