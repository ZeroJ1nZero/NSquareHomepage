using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Application.DTOs;
using Application.UseCases.Admin;
using Application.UseCases.Public;

namespace Web.Controllers;

[ApiController]
public class ServicesController : ControllerBase
{
    private readonly IGetCompanyServiceUseCase _getServiceUseCase;
    private readonly IUpdateCompanyServiceUseCase _updateServiceUseCase;
    private readonly IOidcStateService _oidcStateService;

    public ServicesController(
        IGetCompanyServiceUseCase getServiceUseCase,
        IUpdateCompanyServiceUseCase updateServiceUseCase,
        IOidcStateService oidcStateService)
    {
        _getServiceUseCase = getServiceUseCase;
        _updateServiceUseCase = updateServiceUseCase;
        _oidcStateService = oidcStateService;
    }

    [HttpGet("api/public/company-services")]
    [AllowAnonymous]
    [Tags("2. [파이프라인 2-A] 공개 데이터 조회 (트랙 A: 로그인 불필요)")]
    public async Task<ActionResult<ServiceDto>> GetService(CancellationToken cancellationToken)
    {
        var result = await _getServiceUseCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/admin/company-services")]
    [Tags("3. [파이프라인 2-B] 관리자 데이터 처리 (트랙 B: Role == Admin)")]
    public async Task<IActionResult> UpdateService(
        [FromBody] UpdateServiceDto dto,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole("Admin"))
        {
            var (_, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext, "/api/admin/company-services");
            Response.Headers.Location = authorizeUrl;
            return StatusCode(StatusCodes.Status302Found, new
            {
                message = "관리자 권한(Role == Admin) 인증이 필요합니다. 인증 서버로 302 리다이렉트합니다.",
                code_challenge = challenge,
                state = state,
                authorize_url = authorizeUrl
            });
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var result = await _updateServiceUseCase.ExecuteAsync(dto, accessToken, cancellationToken);
        return Ok(result);
    }
}
