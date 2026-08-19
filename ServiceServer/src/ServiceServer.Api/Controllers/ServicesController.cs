using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceServer.Api.DTOs;
using ServiceServer.Api.Services;

namespace ServiceServer.Api.Controllers;

[ApiController]
public class ServicesController : ControllerBase
{
    private readonly IResourceApiClient _resourceApiClient;
    private readonly IOidcStateService _oidcStateService;

    public ServicesController(IResourceApiClient resourceApiClient, IOidcStateService oidcStateService)
    {
        _resourceApiClient = resourceApiClient;
        _oidcStateService = oidcStateService;
    }

    /// <summary>
    /// [트랙 A: 공개 조회] 주요 서비스 정보 공개 조회 (로그인 불필요 ➔ ResourceServer 대행)
    /// </summary>
    /// <remarks>
    /// 세션 검사 없이 ResourceServer(:7002)의 주요 서비스 정보를 즉시 대행 호출하여 반환합니다.
    /// </remarks>
    [HttpGet("api/public/company-services")]
    [AllowAnonymous]
    [Tags("2. [파이프라인 2-A] 공개 데이터 조회 (트랙 A: 로그인 불필요)")]
    public async Task<ActionResult<ServiceDto>> GetService(CancellationToken cancellationToken)
    {
        var result = await _resourceApiClient.GetServiceAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [트랙 B: 관리자 처리] 주요 서비스 정보 수정 (Role == Admin 검증 + Bearer 토큰 첨부)
    /// </summary>
    /// <remarks>
    /// 1. 관리자 세션 쿠키(`.NsqHomepage.ServiceSession`) 및 `Role == Admin` 권한을 검증합니다.<br/>
    /// 2. 검증 통과 시 세션 내 Access Token을 `Bearer` 헤더로 첨부하여 ResourceServer(:7002)로 전송합니다.<br/>
    /// 3. 미인증 시 **Step 1~2**가 자동 발동하여 PKCE/CSRF 키를 생성하고 IdP 로그인 주소로 **302 리다이렉트**합니다.
    /// </remarks>
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
        var result = await _resourceApiClient.UpdateServiceAsync(dto, accessToken, cancellationToken);
        return Ok(result);
    }
}
