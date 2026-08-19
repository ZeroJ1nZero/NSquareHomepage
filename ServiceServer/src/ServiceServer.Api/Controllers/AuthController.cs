using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceServer.Api.DTOs;
using ServiceServer.Api.Services;

namespace ServiceServer.Api.Controllers;

/// <summary>
/// sso_pipeline_specification.md 규격에 맞춘 OIDC Authorization Code Flow + PKCE SSO 컨트롤러 (BFF 패턴)
/// </summary>
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IOidcStateService _oidcStateService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOidcStateService oidcStateService,
        ILogger<AuthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _oidcStateService = oidcStateService;
        _logger = logger;
    }

    private string IdpBaseUrl => _configuration["Authentication:Authority"] ?? "https://localhost:7213";
    private string ClientId => _configuration["Authentication:ClientId"] ?? "company-homepage";
    private string DefaultRedirectUri => _configuration["Authentication:RedirectUri"] ?? "https://localhost:7001/api/auth/oidc-callback";

    /// <summary>
    /// [Step 1~2] SSO 로그인 시작 (PKCE &amp; CSRF 키 생성 및 IdP 인가 주소 발급)
    /// </summary>
    /// <remarks>
    /// **[시퀀스 흐름]**<br />
    /// 1. **Step 1**: 사용자가 로그인을 시작합니다.<br />
    /// 2. **Step 2**: 서비스 서버가 code_verifier(원본키), code_challenge(해시키), state(CSRF키)를 생성하고 세션에 보관한 뒤 authorize_url을 발급합니다.<br />
    /// 3. **Step 3**: 클라이언트는 반환된 authorize_url로 이동하여 인증 서버(:7213) 로그인을 진행합니다.
    /// </remarks>
    [HttpGet("api/auth/start-sso")]
    [Tags("1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)")]
    public IActionResult Login([FromQuery] string? redirectUri = null)
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

    /// <summary>
    /// [테스트] 아이디/비밀번호 직접 검증 및 세션 발급 (Pipeline 외 보조 기능)
    /// </summary>
    /// <remarks>
    /// Swagger UI 또는 웹 화면에서 아이디와 비밀번호를 직접 입력받아 인증 서버에서 일치 여부를 검증하고, 검증 성공 시 관리자 서비스 세션 쿠키를 발급합니다.
    /// </remarks>
    [HttpPost("api/tools/direct-login")]
    [Tags("4. [기타 / 보조 기능] 세션 관리 및 개발/테스트 도구 (Pipeline 외)")]
    public async Task<IActionResult> LoginWithCredentials(
        [FromBody] LoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new LoginResponseDto(false, "이메일과 비밀번호를 입력해주세요.", null));
        }

        var client = _httpClientFactory.CreateClient("IdpClient");
        var requestUri = $"{IdpBaseUrl.TrimEnd('/')}/api/auth/validate";
        
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(dto),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await client.PostAsync(requestUri, jsonContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("인증 서버 계정 검증 실패: {Email}, 상태코드: {StatusCode}", dto.Email, response.StatusCode);
                return StatusCode((int)response.StatusCode, new LoginResponseDto(false, "아이디 또는 비밀번호가 올바르지 않습니다.", null));
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var userElem = root.GetProperty("user");

            var userId = userElem.GetProperty("id").GetInt64();
            var email = userElem.GetProperty("email").GetString() ?? dto.Email;
            var userName = userElem.GetProperty("userName").GetString() ?? "사용자";
            var role = userElem.GetProperty("role").GetString() ?? "User";

            // 서비스 세션 쿠키 발급 (.NsqHomepage.ServiceSession)
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, userName),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Role, role),
                new("sub", userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
                });

            _logger.LogInformation("아이디/비밀번호 검증 성공 및 세션 발급 완료: {Email} (역할: {Role})", email, role);

            var userInfo = new UserInfoDto(userId, email, userName, role);
            return Ok(new LoginResponseDto(true, $"인증 서버 검증 완료. 환영합니다, {userName}님 ({role})", userInfo));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "인증 서버 통신 중 오류 발생");
            return StatusCode(500, new LoginResponseDto(false, $"인증 서버 통신 실패: {ex.Message}", null));
        }
    }

    /// <summary>
    /// [Step 8~12] OIDC 브라우저 콜백 수신 (CSRF 검증, 백채널 토큰 교환 &amp; 세션 쿠키 발급)
    /// </summary>
    /// <remarks>
    /// **[시퀀스 흐름]**<br />
    /// 1. **Step 8**: 인증서버에서 1회용 인가 코드(code)를 발급하여 브라우저를 /api/auth/oidc-callback 으로 리다이렉트합니다.<br />
    /// 2. **Step 9**: 브라우저가 전달받은 code와 state를 서비스 서버에 제출합니다.<br />
    /// 3. **Step 10**: 서비스 서버가 state 일치 여부를 대조(CSRF 방어)하고, 세션의 code_verifier와 함께 인증 서버로 백채널 토큰 교환을 요청합니다.<br />
    /// 4. **Step 11**: 인증 서버가 PKCE 검증 후 Access/Refresh 토큰을 발급합니다.<br />
    /// 5. **Step 12**: 서비스 서버가 토큰을 내부 세션에 은폐 보관하고 브라우저에 .NsqHomepage.ServiceSession 쿠키를 발급합니다.
    /// </remarks>
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

        var (isSuccess, responseContent, claims, rootElement) = await ProcessTokenExchangeAsync(code, verifier, DefaultRedirectUri, cancellationToken);
        if (!isSuccess)
        {
            _logger.LogError("Back-channel 토큰 교환 실패: {Content}", responseContent);
            return StatusCode(400, JsonSerializer.Deserialize<object>(responseContent));
        }

        // [Step 12] 세션 클린업 및 최종 화면 응답
        var returnUrl = HttpContext.Session.GetString("return_url") ?? "/swagger";
        _oidcStateService.ClearSession(HttpContext);

        var userName = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value ?? "관리자";
        var userRole = claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value ?? "User";

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
    /// <remarks>
    /// Swagger UI에서 인가 코드(`code`)와 `code_verifier`를 직접 입력하여 백채널 토큰 교환 및 세션 쿠키를 발급받을 수 있습니다.
    /// </remarks>
    [HttpPost("api/tools/manual-token-exchange")]
    [Tags("4. [기타 / 보조 기능] 세션 관리 및 개발/테스트 도구 (Pipeline 외)")]
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
        var (isSuccess, responseContent, claims, rootElement) = await ProcessTokenExchangeAsync(request.Code, verifier, redirectUri, cancellationToken);

        if (!isSuccess)
        {
            return StatusCode(400, JsonSerializer.Deserialize<object>(responseContent));
        }

        _oidcStateService.ClearSession(HttpContext);

        return Ok(new
        {
            message = "Back-channel 토큰 교환 완료 및 서비스 세션 쿠키(.NsqHomepage.ServiceSession)가 발급되었습니다.",
            user = new
            {
                sub = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value,
                email = claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value,
                name = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value,
                role = claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value
            },
            tokens = rootElement
        });
    }

    /// <summary>
    /// [Step 12] 현재 세션 사용자 식별 및 Role 확인
    /// </summary>
    /// <remarks>
    /// 발급받은 `.NsqHomepage.ServiceSession` 쿠키를 기반으로 현재 사용자의 Claims(NameIdentifier, Role, Email 등)를 확인합니다.
    /// </remarks>
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
    /// [세션 종료] 서비스 세션 파기 및 AuthServer 전역 SSO 로그아웃 (Pipeline 외)
    /// </summary>
    /// <remarks>
    /// 서비스 세션을 파기하고 인증 서버(`https://localhost:7213/connect/logout`)로 이동하여 모든 사내 서비스의 SSO 세션을 종료합니다.
    /// </remarks>
    [HttpPost("api/auth/global-logout")]
    [Tags("4. [기타 / 보조 기능] 세션 관리 및 개발/테스트 도구 (Pipeline 외)")]
    public async Task<IActionResult> Logout([FromQuery] string? postLogoutRedirectUri = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _oidcStateService.ClearSession(HttpContext);

        var targetPostLogoutUri = string.IsNullOrWhiteSpace(postLogoutRedirectUri)
            ? (_configuration["Authentication:PostLogoutRedirectUri"] ?? "https://localhost:7001/signout-callback-oidc")
            : postLogoutRedirectUri;

        var idpLogoutUrl = $"{IdpBaseUrl.TrimEnd('/')}/connect/logout?post_logout_redirect_uri={Uri.EscapeDataString(targetPostLogoutUri)}";

        if (HttpMethods.IsGet(Request.Method))
        {
            return Redirect(idpLogoutUrl);
        }

        return Ok(new
        {
            message = "서비스 세션이 파기되었습니다. 전역 SSO 세션을 완전히 폐기하려면 idp_logout_url로 이동하세요.",
            idp_logout_url = idpLogoutUrl
        });
    }

    /// <summary>
    /// [로그아웃 완료 화면] 전역 로그아웃 완료 콜백 화면 (/signout-callback-oidc) (Pipeline 외)
    /// </summary>
    [HttpGet("signout-callback-oidc")]
    [Tags("4. [기타 / 보조 기능] 세션 관리 및 개발/테스트 도구 (Pipeline 외)")]
    public IActionResult SignoutCallbackOidc()
    {
        var html = $$"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
                <meta charset="UTF-8">
                <title>전역 로그아웃 완료</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f4f6f9; margin: 0; padding: 40px 20px; color: #333; }
                    .card { max-width: 600px; margin: 0 auto; background: white; padding: 35px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); text-align: center; }
                    h2 { color: #d9534f; margin-top: 0; font-size: 24px; }
                    p { font-size: 15px; line-height: 1.6; color: #555; }
                    .btn { display: inline-block; padding: 12px 24px; background: #007bff; color: white; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 14px; margin-top: 15px; }
                    .btn:hover { background: #0056b3; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h2>🔒 전역 로그아웃 완료</h2>
                    <p>AuthServer SSO 세션과 서비스 세션이 모두 정상적으로 종료되었습니다.</p>
                    <a href="/swagger" class="btn">Swagger UI로 돌아가기</a>
                </div>
            </body>
            </html>
            """;

        return Content(html, "text/html", Encoding.UTF8);
    }

    private async Task<(bool IsSuccess, string Content, List<Claim> Claims, JsonElement RootElement)> ProcessTokenExchangeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("IdpClient");
        var tokenEndpoint = $"{IdpBaseUrl.TrimEnd('/')}/connect/token";

        var parameters = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", ClientId },
            { "code", code },
            { "code_verifier", codeVerifier },
            { "redirect_uri", redirectUri }
        };

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, content, new List<Claim>(), default);
        }

        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement.Clone();

        var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        var idToken = root.TryGetProperty("id_token", out var it) ? it.GetString() : null;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        var claims = new List<Claim>();
        var tokenToRead = idToken ?? accessToken;
        if (!string.IsNullOrWhiteSpace(tokenToRead))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(tokenToRead))
            {
                var jwt = handler.ReadJwtToken(tokenToRead);
                claims.AddRange(jwt.Claims);
            }
        }

        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
        {
            var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "1";
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        if (!claims.Any(c => c.Type == ClaimTypes.Name))
        {
            var name = claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "User";
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        if (!claims.Any(c => c.Type == ClaimTypes.Role))
        {
            var role = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "User";
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

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

        return (true, content, claims, root);
    }
}

public class TokenExchangeRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("code_verifier")]
    public string? CodeVerifier { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("state")]
    public string? State { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }
}
