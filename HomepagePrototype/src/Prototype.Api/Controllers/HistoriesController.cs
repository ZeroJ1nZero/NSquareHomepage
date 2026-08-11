using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.History;
using Prototype.Application.UseCases.History;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/Home/history")]
public class HistoriesController : ControllerBase
{
    /// <summary>
    /// 전체 연혁 목록 조회
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CompanyHistoryDto>>> GetHistories(
        [FromServices] IGetCompanyHistoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 연혁 항목 입력/추가 (SSO 인증 필요 - PUT 전용)
    /// </summary>
    [HttpPut]
    [Authorize]
    public async Task<ActionResult<CompanyHistoryDto>> CreateHistory(
        [FromBody] CreateCompanyHistoryDto dto,
        [FromServices] ICreateCompanyHistoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
