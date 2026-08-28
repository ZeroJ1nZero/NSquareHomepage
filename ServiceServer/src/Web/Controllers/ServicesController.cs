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
    [Tags("공개 데이터 조회")]
    public async Task<ActionResult<ServiceDto>> GetService(CancellationToken cancellationToken)
    {
        var result = await _getServiceUseCase.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/admin/company-services")]
    [Tags("관리자 리소스 CRUD")]
    public async Task<IActionResult> UpdateService(
        [FromBody] UpdateServiceDto dto,
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
            var authResult = await HttpContext.AuthenticateAsync("Cookie_Service");
            if (authResult.Succeeded && authResult.Principal?.IsInRole("Admin") == true)
            {
                accessToken = authResult.Properties?.GetTokenValue("access_token")
                              ?? HttpContext.Session.GetString("access_token_service")
                              ?? HttpContext.Session.GetString("access_token");
            }
        }

        // 주요 서비스 전용 세션 쿠키(.Nsq.Service.Session) 또는 유효한 관리자 토큰이 존재하지 않는 경우:
        // PKCE를 생성하고 returnUrl = /service, targetService = service로 302 Found 반환하여 Silent SSO 가동
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            var returnUrl = Request.Headers.Referer.ToString();
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "http://localhost:3000/service";
            }
            var (verifier, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext, returnUrl, "service");
            Response.Headers.Location = authorizeUrl;
            return StatusCode(StatusCodes.Status302Found, new
            {
                message = "주요 서비스 전용 세션 쿠키(.Nsq.Service.Session)가 존재하지 않아 인증 서버(IdP)로 리다이렉트합니다.",
                service = "service",
                authorize_url = authorizeUrl,
                client_id = "company-homepage",
                response_type = "code",
                code_challenge = challenge,
                code_challenge_method = "S256",
                state = state,
                scope = "openid profile email roles offline_access"
            });
        }

        var result = await _updateServiceUseCase.ExecuteAsync(dto, accessToken, cancellationToken);
        return Ok(result);
    }
}
