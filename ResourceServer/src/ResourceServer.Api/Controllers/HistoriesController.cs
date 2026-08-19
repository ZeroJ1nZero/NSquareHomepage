using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceServer.Application.DTOs.History;
using ResourceServer.Application.UseCases.History;

namespace ResourceServer.Api.Controllers;

[ApiController]
[Route("api/Home/history")]
public class HistoriesController : ControllerBase
{
    /// <summary>
    /// [트랙 A: 공개 조회] 전체 연혁 목록 조회 (인증 불필요)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HistoryContainerDto>> GetHistories(
        [FromServices] IGetCompanyHistoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        var historyItems = result.Select(h => new HistoryItemDto(h.Date, h.Content)).ToList();
        return Ok(new HistoryContainerDto(historyItems));
    }

    /// <summary>
    /// [트랙 B: 관리자 처리] 연혁 목록 저장/추가 (JWT Bearer + Admin 권한 필요)
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HistoryContainerDto>> SaveHistories(
        [FromBody] SaveHistoryRequestDto dto,
        [FromServices] ICreateCompanyHistoryUseCase createUseCase,
        [FromServices] IGetCompanyHistoriesUseCase getUseCase,
        CancellationToken cancellationToken)
    {
        if (dto.History != null && dto.History.Count > 0)
        {
            foreach (var item in dto.History)
            {
                await createUseCase.ExecuteAsync(new CreateCompanyHistoryDto(item.Data, item.Content), cancellationToken);
            }
        }

        var result = await getUseCase.ExecuteAsync(cancellationToken);
        var historyItems = result.Select(h => new HistoryItemDto(h.Date, h.Content)).ToList();
        return Ok(new HistoryContainerDto(historyItems));
    }
}
