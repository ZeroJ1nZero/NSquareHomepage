using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.History;
using Prototype.Application.UseCases.History;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// 연혁 항목 입력/추가 (SSO 인증 필요 - PUT & POST 모두 지원)
    /// </summary>
    [HttpPut]
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CompanyHistoryDto>> CreateHistory(
        [FromBody] CreateCompanyHistoryDto dto,
        [FromServices] ICreateCompanyHistoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }

    /* ── [향후 단건 상세/수정/삭제 필요 시 주석 해제하여 사용 가능] ──
    
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompanyHistoryDto>> GetHistoryById(int id, [FromServices] IGetCompanyHistoryByIdUseCase useCase, CancellationToken ct) { ... }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompanyHistoryDto>> UpdateHistory(int id, UpdateCompanyHistoryDto dto, [FromServices] IUpdateCompanyHistoryUseCase useCase, CancellationToken ct) { ... }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHistory(int id, [FromServices] IDeleteCompanyHistoryUseCase useCase, CancellationToken ct) { ... }

    ── [끝] ── */
}
