using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Application.DTOs;
using Application.UseCases.Admin;
using Application.UseCases.Public;

namespace Web.Controllers;

[ApiController]
public class AboutController : ControllerBase
{
    private readonly IGetCompanyAboutUseCase _getAboutUseCase;
    private readonly IUpdateCompanyAboutUseCase _updateAboutUseCase;
    private readonly IOidcStateService _oidcStateService;

    public AboutController(
        IGetCompanyAboutUseCase getAboutUseCase,
        IUpdateCompanyAboutUseCase updateAboutUseCase,
        IOidcStateService oidcStateService)
    {
        _getAboutUseCase = getAboutUseCase;
        _updateAboutUseCase = updateAboutUseCase;
        _oidcStateService = oidcStateService;
    }

    [HttpGet("api/public/company-about")]
    [AllowAnonymous]
    [Tags("2. [파이프라인 2-A] 공개 데이터 조회 (트랙 A: 로그인 불필요)")]
    public async Task<ActionResult<AboutDto>> GetAbout(CancellationToken cancellationToken)
    {
        var result = await _getAboutUseCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/admin/company-about")]
    [Tags("3. [파이프라인 2-B] 관리자 데이터 처리 (트랙 B: Role == Admin)")]
    public async Task<IActionResult> UpdateAbout(
        [FromBody] UpdateAboutDto dto,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole("Admin"))
        {
            var (_, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext, "/api/admin/company-about");
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
        var result = await _updateAboutUseCase.ExecuteAsync(dto, accessToken, cancellationToken);
        return Ok(result);
    }
}
