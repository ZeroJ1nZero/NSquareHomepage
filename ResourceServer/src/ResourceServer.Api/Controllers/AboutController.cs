using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceServer.Application.DTOs.About;
using ResourceServer.Application.UseCases.About;

namespace ResourceServer.Api.Controllers;

[ApiController]
[Route("api/Home/about")]
public class AboutController : ControllerBase
{
    /// <summary>
    /// [트랙 A: 공개 조회] 회사 소개 정보 조회 (인증 불필요)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<AboutDto>> GetAbout(
        [FromServices] IGetAboutUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [트랙 B: 관리자 처리] 회사 소개 정보 수정 (JWT Bearer + Admin 권한 필요)
    /// </summary>
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
