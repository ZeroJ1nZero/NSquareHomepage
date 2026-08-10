using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.CompanyHistory;
using Prototype.Application.UseCases.CompanyHistory;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoriesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CompanyHistoryDto>>> GetHistories(
        [FromServices] IGetCompanyHistoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompanyHistoryDto>> GetHistoryById(
        [FromRoute] int id,
        [FromServices] IGetCompanyHistoryByIdUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = $"ID {id} 연혁 항목을 찾을 수 없습니다." });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CompanyHistoryDto>> CreateHistory(
        [FromBody] CreateCompanyHistoryDto dto,
        [FromServices] ICreateCompanyHistoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetHistoryById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompanyHistoryDto>> UpdateHistory(
        [FromRoute] int id,
        [FromBody] UpdateCompanyHistoryDto dto,
        [FromServices] IUpdateCompanyHistoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, dto, cancellationToken);
        if (result == null) return NotFound(new { message = $"ID {id} 연혁 항목을 찾을 수 없습니다." });

        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHistory(
        [FromRoute] int id,
        [FromServices] IDeleteCompanyHistoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var success = await useCase.ExecuteAsync(id, cancellationToken);
        if (!success) return NotFound(new { message = $"ID {id} 연혁 항목을 찾을 수 없습니다." });

        return NoContent();
    }
}
