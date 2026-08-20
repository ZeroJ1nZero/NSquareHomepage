using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Application.DTOs;

namespace Web.Controllers;

/// <summary>
/// sso_pipeline_specification.md 규격에 맞춘 OIDC Authorization Code Flow + PKCE SSO 컨트롤러 (BFF 패턴)
/// </summary>
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IOidcStateService _oidcStateService;
    private readonly IOidcTokenExchangeService _tokenExchangeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOidcStateService oidcStateService,
        IOidcTokenExchangeService tokenExchangeService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _oidcStateService = oidcStateService;
        _tokenExchangeService = tokenExchangeService;
        _configuration = configuration;
        _logger = logger;
    }

    private string DefaultRedirectUri => _configuration["Authentication:RedirectUri"] ?? "https://localhost:7001/api/auth/oidc-callback";

    [HttpGet("api/auth/start-sso")]
    [Tags("1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)")]
    public IActionResult Login()
    {
        var (verifier, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(HttpContext);

        _logger.LogInformation("SSO 로그인 요청 생성 완료 - State: {State}, CodeChallenge: {Challenge}", state, challenge);

        if (Request.Headers.Accept.ToString().Contains("text/html"))
        {
            return Redirect(authorizeUrl);
        }

        return Ok(new
        {
            code_challenge = challenge,
            code_verifier = verifier,
            code_challenge_method = "S256",
            state = state,
            authorize_url = authorizeUrl
        });
    }

    [HttpGet("api/auth/oidc-callback")]
    [Tags("1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)")]
    public async Task<IActionResult> SigninOidcGet(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogError("IdP 인증 실패: {Error} - {Description}", error, error_description);
            return BadRequest(new { error, error_description });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { message = "인가 코드(code)가 제공되지 않았습니다." });
        }

        // [Step 9~10] CSRF 검증키(state) 확인
        if (!_oidcStateService.ValidateCsrfState(HttpContext, state))
        {
            _logger.LogWarning("CSRF 검증 실패: 전달된 state({State})가 세션에 저장된 state와 일치하지 않거나 세션이 만료되었습니다.", state);
            return BadRequest(new { message = "CSRF 보안 검증 실패: state 파라미터가 유효하지 않습니다." });
        }

        // [Step 10] 세션에 보관된 PKCE 원본키(code_verifier) 추출 후 Back-channel 토큰 교환 요청
        var verifier = _oidcStateService.GetStoredVerifier(HttpContext);
        if (string.IsNullOrWhiteSpace(verifier))
        {
            _logger.LogWarning("세션 내 PKCE 원본키(code_verifier)를 찾을 수 없습니다.");
            return BadRequest(new { message = "세션이 만료되었거나 PKCE 원본키가 존재하지 않습니다. 다시 로그인을 시도해 주세요." });
        }

        var result = await _tokenExchangeService.ExchangeCodeForTokensAsync(code, verifier, DefaultRedirectUri, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger.LogError("Back-channel 토큰 교환 실패: {Content}", result.ResponseContent);
            return StatusCode(400, JsonSerializer.Deserialize<object>(result.ResponseContent));
        }

        // 서비스 세션 쿠키 발급
        await IssueServiceSessionCookieAsync(result);

        // [Step 12] 세션 클린업 및 최종 화면 응답
        _oidcStateService.ClearSession(HttpContext);

        var userName = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value ?? "관리자";
        var userRole = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value ?? "User";

        var html = $$"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
                <meta charset="UTF-8">
                <title>로그인 완료 - ServiceServer</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f4f6f9; margin: 0; padding: 40px 20px; color: #333; }
                    .card { max-width: 600px; margin: 0 auto; background: white; padding: 35px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); text-align: center; }
                    h2 { color: #28a745; margin-top: 0; font-size: 24px; }
                    p { font-size: 15px; line-height: 1.6; color: #555; }
                    .badge { display: inline-block; padding: 6px 12px; background: #e8f5e9; color: #2e7d32; border-radius: 20px; font-size: 13px; font-weight: bold; margin: 10px 0 20px 0; }
                    .info-box { background: #f8f9fa; border: 1px solid #e9ecef; padding: 15px; border-radius: 8px; font-size: 14px; text-align: left; margin-bottom: 25px; }
                    .btn { display: inline-block; padding: 12px 24px; background: #007bff; color: white; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 14px; border: none; cursor: pointer; transition: background 0.2s; }
                    .btn:hover { background: #0056b3; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h2>🎉 SSO 로그인 완료!</h2>
                    <span class="badge">BFF 세션 쿠키 발급 완료 (.NsqHomepage.ServiceSession)</span>
                    <p><strong>{{userName}}</strong>님 (역할: <strong>{{userRole}}</strong>) 환영합니다.</p>
                    
                    <div class="info-box">
                        • <strong>[Step 9~10] CSRF 검증키(state) 대조</strong>: 일치 (위조 공격 차단 성공)<br/>
                        • <strong>[Step 10~11] Back-channel 토큰 교환</strong>: 성공 (Access Token 획득 및 세션 보관)<br/>
                        • <strong>[Step 12] 브라우저 세션 쿠키 발급</strong>: 완료 (XSS 방어 완료)
                    </div>

                    <div style="margin-top: 25px;">
                        <a href="/" class="btn" style="background: #2563eb; margin-right: 10px;">🏠 홈페이지로 이동</a>
                        <a href="/swagger" class="btn" style="background: #64748b;">Swagger UI</a>
                    </div>
                </div>
                <script>
                    setTimeout(() => { window.location.href = "/"; }, 2500);
                </script>
            </body>
            </html>
            """;

        return Content(html, "text/html", Encoding.UTF8);
    }

    /// <summary>
    /// [테스트] Swagger/Postman용 인가 코드 + verifier 수동 토큰 교환 (Pipeline 외 보조 기능)
    /// </summary>
    [HttpPost("api/tools/manual-token-exchange")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SigninOidcPost([FromBody] TokenExchangeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "code는 필수 값입니다." });
        }

        var verifier = _oidcStateService.GetStoredVerifier(HttpContext) ?? request.CodeVerifier;
        if (string.IsNullOrWhiteSpace(verifier))
        {
            return BadRequest(new { message = "code_verifier를 찾을 수 없습니다. 요청 Body에 포함하거나 세션이 활성화되어 있어야 합니다." });
        }

        if (!string.IsNullOrWhiteSpace(request.State) && !_oidcStateService.ValidateCsrfState(HttpContext, request.State))
        {
            return BadRequest(new { message = "CSRF 보안 검증 실패: state가 일치하지 않습니다." });
        }

        var redirectUri = string.IsNullOrWhiteSpace(request.RedirectUri) ? DefaultRedirectUri : request.RedirectUri;
        var result = await _tokenExchangeService.ExchangeCodeForTokensAsync(request.Code, verifier, redirectUri, cancellationToken);

        if (!result.IsSuccess)
        {
            return StatusCode(400, JsonSerializer.Deserialize<object>(result.ResponseContent));
        }

        await IssueServiceSessionCookieAsync(result);
        _oidcStateService.ClearSession(HttpContext);

        return Ok(new
        {
            message = "Back-channel 토큰 교환 완료 및 서비스 세션 쿠키(.NsqHomepage.ServiceSession)가 발급되었습니다.",
            user = new
            {
                sub = result.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value,
                email = result.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value,
                name = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value,
                role = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value
            },
            tokens = result.RootElement
        });
    }

    [HttpGet("api/auth/user-identity")]
    [Tags("1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public IActionResult GetCurrentUser()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(new
        {
            isAuthenticated = User.Identity?.IsAuthenticated ?? false,
            authenticationType = User.Identity?.AuthenticationType,
            userName = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value,
            claims = claims
        });
    }

    /// <summary>
    /// Refresh Token을 이용하여 Access Token 재발급 및 세션 갱신
    /// </summary>
    [HttpPost("api/auth/refresh")]
    [Tags("1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("세션 내에 Refresh Token이 존재하지 않습니다.");
            return BadRequest(new { message = "세션 내에 유효한 Refresh Token이 존재하지 않습니다. 다시 로그인해 주세요." });
        }

        var result = await _tokenExchangeService.RefreshTokensAsync(refreshToken, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger.LogError("Refresh Token으로 Access Token 갱신 실패: {Content}", result.ResponseContent);
            return StatusCode(400, JsonSerializer.Deserialize<object>(result.ResponseContent));
        }

        // 새 토큰들로 세션 쿠키 업데이트
        await IssueServiceSessionCookieAsync(result);

        return Ok(new
        {
            success = true,
            message = "Access Token이 Refresh Token으로부터 성공적으로 갱신되었습니다.",
            expires_in = result.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 900,
            user = new
            {
                sub = result.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                name = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value ?? User.Identity?.Name,
                role = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value ?? User.FindFirstValue(ClaimTypes.Role)
            }
        });
    }

    /// <summary>
    /// [세션 종료] 서비스 세션 파기 로그아웃 (Pipeline 외 보조 기능)
    /// </summary>
    [HttpPost("api/auth/logout")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _oidcStateService.ClearSession(HttpContext);

        return Ok(new
        {
            success = true,
            message = "서비스 세션 쿠키(.NsqHomepage.ServiceSession)가 성공적으로 파기되었습니다."
        });
    }

    private async Task IssueServiceSessionCookieAsync(TokenExchangeResultDto result)
    {
        var claims = new List<Claim>(result.Claims);

        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
        {
            var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1";
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        if (!claims.Any(c => c.Type == ClaimTypes.Name))
        {
            var name = claims.FirstOrDefault(c => c.Type == "name")?.Value ?? User.Identity?.Name ?? "User";
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        if (!claims.Any(c => c.Type == ClaimTypes.Role))
        {
            var role = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? User.FindFirstValue(ClaimTypes.Role) ?? "User";
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var accessToken = result.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        var refreshToken = result.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        }

        var authProperties = new AuthenticationProperties();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            authProperties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = accessToken },
                new AuthenticationToken { Name = "refresh_token", Value = refreshToken ?? "" }
            });
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }
}
