using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.Service;
using Prototype.Application.UseCases.Service;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/Home/service")]
public class ServicesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ServiceDto>> GetService(
        [FromServices] IGetServiceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize] // nsq_auth SSO 인증 필요
    public async Task<ActionResult<ServiceDto>> UpdateService(
        [FromBody] UpdateServiceDto dto,
        [FromServices] IUpdateServiceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
