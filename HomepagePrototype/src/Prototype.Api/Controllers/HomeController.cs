using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.CompanyInfo;
using Prototype.Application.DTOs.CompanyHistory;
using Prototype.Application.UseCases.CompanyInfo;
using Prototype.Application.UseCases.CompanyHistory;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    // Postman 1: GET /api/Home/about
    [HttpGet("about")]
    public async Task<ActionResult> GetAbout(
        [FromServices] IGetCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(new { introduction = result.Introduction });
    }

    // Postman 2: PUT /api/Home/about
    [HttpPut("about")]
    public async Task<ActionResult> UpdateAbout(
        [FromBody] AboutRequest request,
        [FromServices] IUpdateCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new UpdateCompanyInfoDto(request.Introduction, null), cancellationToken);
        return Ok(new { introduction = result.Introduction });
    }

    // Postman 3: GET /api/Home/service
    [HttpGet("service")]
    public async Task<ActionResult> GetService(
        [FromServices] IGetCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(new { service = result.Service });
    }

    // Postman 4: PUT /api/Home/service
    [HttpPut("service")]
    public async Task<ActionResult> UpdateService(
        [FromBody] ServiceRequest request,
        [FromServices] IUpdateCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new UpdateCompanyInfoDto(null, request.Service), cancellationToken);
        return Ok(new { service = result.Service });
    }

    // Postman 5: GET /api/Home/history
    [HttpGet("history")]
    public async Task<ActionResult> GetHistory(
        [FromServices] IGetCompanyHistoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var histories = await useCase.ExecuteAsync(cancellationToken);
        var list = histories.Select(h => new { date = h.Date.ToString("yyyy-MM-dd"), content = h.Content }).ToList();
        return Ok(new { history = list });
    }

    // Postman 6: PUT /api/Home/history
    [HttpPut("history")]
    public async Task<ActionResult> UpdateHistory(
        [FromBody] HistoryListRequest request,
        [FromServices] IGetCompanyHistoriesUseCase getUseCase,
        [FromServices] ICreateCompanyHistoryUseCase createUseCase,
        [FromServices] IDeleteCompanyHistoryUseCase deleteUseCase,
        CancellationToken cancellationToken)
    {
        var existing = await getUseCase.ExecuteAsync(cancellationToken);
        foreach (var item in existing)
        {
            await deleteUseCase.ExecuteAsync(item.Id, cancellationToken);
        }

        if (request.History != null)
        {
            foreach (var item in request.History)
            {
                await createUseCase.ExecuteAsync(new CreateCompanyHistoryDto(item.Date, item.Content), cancellationToken);
            }
        }

        var updated = await getUseCase.ExecuteAsync(cancellationToken);
        var list = updated.Select(h => new { date = h.Date.ToString("yyyy-MM-dd"), content = h.Content }).ToList();
        return Ok(new { history = list });
    }
}

public record AboutRequest(string Introduction);
public record ServiceRequest(string Service);
public record HistoryItemRequest(DateOnly Date, string Content);
public record HistoryListRequest(List<HistoryItemRequest> History);
