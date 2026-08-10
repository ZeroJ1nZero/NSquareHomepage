using Microsoft.AspNetCore.Mvc;
using Prototype.Application.DTOs.CompanyInfo;
using Prototype.Application.UseCases.CompanyInfo;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/company-info")]
public class CompanyInfoController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CompanyInfoDto>> GetCompanyInfo(
        [FromServices] IGetCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<CompanyInfoDto>> UpdateCompanyInfo(
        [FromBody] UpdateCompanyInfoDto dto,
        [FromServices] IUpdateCompanyInfoUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(dto, cancellationToken);
        return Ok(result);
    }
}
