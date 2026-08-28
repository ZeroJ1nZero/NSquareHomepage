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
    [Tags("공개 데이터 조회")]
    public async Task<ActionResult<AboutDto>> GetAbout(CancellationToken cancellationToken)
    {
        var result = await _getAboutUseCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/admin/company-about")]
    [Tags("관리자 리소스 CRUD")]
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
        else
        {
            var authResult = await HttpContext.AuthenticateAsync("Cookie_About");
            if (authResult.Succeeded && authResult.Principal?.IsInRole("Admin") == true)
            {
                accessToken = authResult.Properties?.GetTokenValue("access_token")
                              ?? HttpContext.Session.GetString("access_token_about")
                              ?? HttpContext.Session.GetString("access_token");
            }
        }

        // 회사 소개 전용 서비스 세션 쿠키(.Nsq.About.Session) 또는 유효한 관리자 토큰이 존재하지 않는 경우:
        // PKCE를 생성하고 returnUrl = /about, targetService = about로 302 Found 반환하여 Silent SSO 가동
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            var returnUrl = Request.Headers.Referer.ToString();
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "http://localhost:3000/about";
            }
            var (verifier, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext, returnUrl, "about");
            Response.Headers.Location = authorizeUrl;
            return StatusCode(StatusCodes.Status302Found, new
            {
                message = "회사 소개 서비스 세션 쿠키(.Nsq.About.Session)가 존재하지 않아 인증 서버(IdP)로 리다이렉트합니다.",
                service = "about",
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
