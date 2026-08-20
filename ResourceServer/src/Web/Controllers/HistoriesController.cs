using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.History;
using Application.UseCases.History;

namespace Web.Controllers;

[ApiController]
[Route("api/Home/history")]
public class HistoriesController : ControllerBase
{
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
