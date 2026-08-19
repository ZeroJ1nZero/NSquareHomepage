using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.Service;
using Application.UseCases.Service;

namespace Api.Controllers;

[ApiController]
[Route("api/Home/service")]
public class ServicesController : ControllerBase
{
    /// <summary>
    /// [트랙 A: 공개 조회] 회사 서비스 정보 조회 (인증 불필요)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ServiceDto>> GetService(
        [FromServices] IGetServiceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [트랙 B: 관리자 처리] 회사 서비스 정보 수정 (JWT Bearer + Admin 권한 필요)
    /// </summary>
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
