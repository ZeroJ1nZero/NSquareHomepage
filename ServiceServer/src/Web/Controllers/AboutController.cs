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
    [Tags("공개 데이터 조회 (Public API - 트랙 A)")]
    public async Task<ActionResult<AboutDto>> GetAbout(CancellationToken cancellationToken)
    {
        var result = await _getAboutUseCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/admin/company-about")]
    [Tags("Step 10. Zero-Trust 관리자 리소스 CRUD (소개 / 서비스 / 연혁)")]
    public async Task<IActionResult> UpdateAbout(
        [FromBody] UpdateAboutDto dto,
        CancellationToken cancellationToken)
    {
        string? accessToken = null;
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = authHeader["Bearer ".Length..].Trim();
        }
        else if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            accessToken = await HttpContext.GetTokenAsync("access_token")
                          ?? HttpContext.Session.GetString("access_token");
        }

        // 서비스세션쿠키 또는 유효한 관리자 토큰이 존재하지 않는 경우:
        // PKCE 원본키(code_verifier), 해시키(code_challenge), CSRF 검증키(state)를 생성하고
        // 서버 세션에 PKCE 원본키와 CSRF 검증키를 저장한 후,
        // 클라이언트에게 인증서버 리다이렉트 주소(해시키, CSRF 검증키, 서비스식별자, response_type=code, scope 포함)를 전달
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            var returnUrl = Request.Headers.Referer.ToString();
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "http://localhost:3000/about";
            }
            var (verifier, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext, returnUrl);
            Response.Headers.Location = authorizeUrl;
            return StatusCode(StatusCodes.Status302Found, new
            {
                message = "서비스 세션 쿠키가 존재하지 않아 인증 서버(IdP)로 리다이렉트합니다.",
                authorize_url = authorizeUrl,
                client_id = "company-homepage",
                response_type = "code",
                code_challenge = challenge,
                code_challenge_method = "S256",
                state = state,
                scope = "openid profile email roles offline_access"
            });
        }

        var result = await _updateAboutUseCase.ExecuteAsync(dto, accessToken, cancellationToken);
        return Ok(result);
    }
}
