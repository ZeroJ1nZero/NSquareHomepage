using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.History;
using Application.UseCases.History;

namespace Web.Controllers;

[ApiController]
[Route("api/Home/history")]
[Tags("회사 연혁 (Company History)")]
public class HistoriesController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HistoryContainerDto>> GetHistories(
        [FromServices] IGetCompanyHistoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        var historyItems = result.Select(h => new HistoryItemDto(h.Id, h.EventDate, h.Content)).ToList();
        return Ok(new HistoryContainerDto(historyItems));
    }

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
                await createUseCase.ExecuteAsync(new CreateCompanyHistoryDto(item.EventDate, item.Content), cancellationToken);
            }
        }

        var result = await getUseCase.ExecuteAsync(cancellationToken);
        var historyItems = result.Select(h => new HistoryItemDto(h.Id, h.EventDate, h.Content)).ToList();
        return Ok(new HistoryContainerDto(historyItems));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteHistory(
        int id,
        [FromServices] IDeleteCompanyHistoryUseCase deleteUseCase,
        CancellationToken cancellationToken)
    {
        var success = await deleteUseCase.ExecuteAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = $"ID {id}인 연혁 항목을 찾을 수 없습니다." });
        }
        return Ok(new { success = true, message = $"연혁(ID: {id}) 항목이 삭제되었습니다." });
    }
}
