using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.Service;
using Application.UseCases.Service;

namespace Web.Controllers;

[ApiController]
[Route("api/Home/service")]
[Tags("주요 서비스 (Company Services)")]
public class ServicesController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ServiceDto>> GetService(
        [FromServices] IGetServiceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ServiceDto>> UpdateService(
        [FromBody] UpdateServiceDto dto,
        [FromServices] IUpdateServiceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
