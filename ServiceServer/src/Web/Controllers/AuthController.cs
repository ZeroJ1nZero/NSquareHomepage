using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Application.DTOs;
using Application.UseCases.Auth;

namespace Web.Controllers;

/// <summary>
/// sso_pipeline_specification.md 규격에 맞춘 OIDC Authorization Code Flow + PKCE SSO 컨트롤러 (Clean Architecture 및 SOLID 원칙 준수)
/// </summary>
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IInitiateSsoUseCase _initiateSsoUseCase;
    private readonly IVerifyCsrfStateUseCase _verifyCsrfStateUseCase;
    private readonly IExchangeTokenUseCase _exchangeTokenUseCase;
    private readonly IOidcStateService _oidcStateService;
    private readonly IOidcTokenExchangeService _tokenExchangeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IInitiateSsoUseCase initiateSsoUseCase,
        IVerifyCsrfStateUseCase verifyCsrfStateUseCase,
        IExchangeTokenUseCase exchangeTokenUseCase,
        IOidcStateService oidcStateService,
        IOidcTokenExchangeService tokenExchangeService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _initiateSsoUseCase = initiateSsoUseCase;
        _verifyCsrfStateUseCase = verifyCsrfStateUseCase;
        _exchangeTokenUseCase = exchangeTokenUseCase;
        _oidcStateService = oidcStateService;
        _tokenExchangeService = tokenExchangeService;
        _configuration = configuration;
        _logger = logger;
    }

    private string DefaultRedirectUri => _configuration["Authentication:RedirectUri"] ?? "https://localhost:7001/api/auth/oidc-callback";

    /// <summary>
    /// [SSO 시작 및 PKCE/CSRF 키 생성] 서비스 세션 쿠키 검증 및 미보유 시 인증 서버(IdP) 인가 주소와 PKCE/CSRF 키 반환
    /// </summary>
    /// <remarks>
    /// 1. 암호학적 난수로 **PKCE 원본키(`pkce_verifier`)**, **해시키(`code_challenge`)**, **CSRF 검증키(`state`)**를 생성합니다.
    /// 2. 서비스 서버 세션 메모리에 `pkce_verifier`와 `oauth_state`를 안전하게 저장합니다.
    /// 3. 클라이언트(Swagger UI/SPA)에게 인증 서버로 이동할 **`authorizeUrl` 및 파라미터 일체**를 JSON(200 OK)으로 반환합니다.
    /// </remarks>
    /// <param name="returnUrl">로그인 완료 후 최종 복귀할 페이지 주소 (기본: /)</param>
    /// <param name="autoRedirect">브라우저 자동 302 리다이렉트 여부 (기본: false, Swagger UI 확인 시 JSON 200 OK 반환)</param>
    [HttpGet("api/auth/start-sso")]
    [Tags("Step 1. SSO 시작 & PKCE/CSRF 키 발급 (Initiate SSO)")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StartSsoResultDto), StatusCodes.Status200OK)]
    public IActionResult StartSso([FromQuery] string? returnUrl = null, [FromQuery] bool autoRedirect = false)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            var targetReturn = returnUrl ?? "/";
            if (autoRedirect)
            {
                return Redirect(targetReturn);
            }
            return Ok(new
            {
                success = true,
                isAuthenticated = true,
                message = "이미 유효한 서비스 세션 쿠키(.NsqHomepage.ServiceSession)를 보유하고 있습니다.",
                userName = User.Identity?.Name,
                role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin"
            });
        }

        var result = _initiateSsoUseCase.Execute(HttpContext, returnUrl);

        _logger.LogInformation("SSO 로그인 요청 생성 완료 - State: {State}, CodeChallenge: {Challenge}, AuthorizeUrl: {Url}", result.State, result.CodeChallenge, result.AuthorizeUrl);

        Response.Headers.Location = result.AuthorizeUrl;
        if (autoRedirect)
        {
            return Redirect(result.AuthorizeUrl);
        }

        return Ok(result);
    }

    /// <summary>
    /// [OIDC 콜백 처리 및 세션 쿠키 발급] 토큰 발급 수신 완료 후 클라이언트에 서비스 세션 쿠키 발급 및 복귀
    /// </summary>
    /// <remarks>
    /// 1. 인가 코드(`code`)와 CSRF 검증키(`state`)를 수신합니다.
    /// 2. 인증 서버(`POST :7213/connect/token`)와 백채널 통신하여 Access/Refresh Token을 발급받습니다.
    /// 3. 발급받은 토큰을 서비스 서버 세션 메모리에 보관하고, 브라우저에 `.NsqHomepage.ServiceSession` 쿠키를 발급합니다.
    /// </remarks>
    /// <param name="code">인증 서버가 발급한 일회용 인가 코드</param>
    /// <param name="state">CSRF 방어용 state 값 (서비스 서버 세션과 일치해야 함)</param>
    /// <param name="error">인증 서버 에러 코드 (있을 경우)</param>
    /// <param name="error_description">인증 서버 에러 상세 설명</param>
    /// <param name="cancellationToken">취소 토큰</param>
    [HttpGet("api/auth/oidc-callback")]
    [Tags("Step 7. 서비스 세션 쿠키 발급 및 OIDC 콜백 (Issue .NsqHomepage.ServiceSession Cookie)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return BadRequest(new { success = false, error, error_description });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { success = false, message = "인가 코드(code)가 제공되지 않았습니다." });
        }

        // [CSRF 검증] state 파라미터가 서비스 서버 세션(oauth_state)에 저장된 값과 일치하는지 확인
        if (!_oidcStateService.ValidateCsrfState(HttpContext, state))
        {
            _logger.LogWarning("CSRF 검증 실패: 전달된 state({State})가 서비스 서버 세션에 저장된 state와 일치하지 않거나 세션이 만료되었습니다.", state);
            return BadRequest(new
            {
                success = false,
                error = "csrf_validation_failed",
                message = "CSRF 보안 검증 실패: state 파라미터가 서비스 서버 세션에 저장된 값과 일치하지 않거나 세션이 만료되었습니다.",
                incomingState = state,
                stateMatched = false
            });
        }

        _logger.LogInformation("CSRF state 검증 성공 - State: {State}", state);

        // [PKCE Verifier 추출] 세션에 보관된 PKCE 원본키(code_verifier) 추출
        var verifier = _oidcStateService.GetStoredVerifier(HttpContext);
        if (string.IsNullOrWhiteSpace(verifier))
        {
            _logger.LogWarning("세션 내 PKCE 원본키(code_verifier)를 찾을 수 없습니다.");
            return BadRequest(new
            {
                success = false,
                error = "missing_pkce_verifier",
                message = "세션이 만료되었거나 PKCE 원본키가 존재하지 않습니다. 다시 로그인을 시도해 주세요."
            });
        }

        // [Back-Channel 토큰 교환]
        var result = await _tokenExchangeService.ExchangeCodeForTokensAsync(code, verifier, DefaultRedirectUri, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger.LogError("Back-channel 토큰 교환 실패: {Content}", result.ResponseContent);
            return StatusCode(400, JsonSerializer.Deserialize<object>(result.ResponseContent));
        }

        // 서비스 세션 쿠키 발급
        await IssueServiceSessionCookieAsync(result);

        var returnUrl = HttpContext.Session.GetString("return_url") ?? "http://localhost:3000/";
        if (!returnUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !returnUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            returnUrl = $"http://localhost:3000{returnUrl}";
        }

        // 세션 클린업
        _oidcStateService.ClearSession(HttpContext);

        if (Request.Headers.Accept.ToString().Contains("text/html") || Request.Query.ContainsKey("code"))
        {
            return Redirect(returnUrl);
        }

        return Ok(new
        {
            success = true,
            message = "서비스 세션 쿠키(.NsqHomepage.ServiceSession) 발급 및 최종 인증 완료",
            sessionCookie = new
            {
                name = ".NsqHomepage.ServiceSession",
                isIssued = true,
                httpOnly = true,
                secure = true,
                sameSite = "Lax",
                expiresInMinutes = 15
            },
            sessionMemory = new
            {
                isStored = true,
                storageType = "DistributedMemoryCache (ISession)",
                storedTokens = new[] { "access_token", "id_token", "refresh_token" }
            },
            user = new
            {
                sub = result.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value,
                email = result.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value,
                name = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value,
                role = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value,
                isAuthenticated = true
            },
            stateValidated = true,
            tokens = result.RootElement,
            redirectUrl = returnUrl
        });
    }

    /// <summary>
    /// [CSRF State 검증 확인] 특정 state 값이 현재 서비스 서버 세션과 일치하는지 단독 검증
    /// </summary>
    /// <param name="state">검증할 state 값 (생략 시 세션에 저장된 oauth_state 값으로 자동 검증)</param>
    [HttpGet("api/auth/verify-state")]
    [Tags("Step 5. CSRF State 일치 검증 (Verify CSRF State)")]
    [ProducesResponseType(typeof(VerifyStateResultDto), StatusCodes.Status200OK)]
    public IActionResult VerifyState([FromQuery] string? state = null)
    {
        var targetState = !string.IsNullOrWhiteSpace(state)
            ? state
            : _oidcStateService.GetStoredVerifier(HttpContext) != null ? (HttpContext.Session.TryGetValue("oauth_state", out var b) ? System.Text.Encoding.UTF8.GetString(b) : "") : "";

        var result = _verifyCsrfStateUseCase.Execute(HttpContext, targetState);
        return Ok(result);
    }

    /// <summary>
    /// [서버 직통신 토큰 교환] 인가 코드 및 PKCE 원본키(code_verifier) Back-channel 토큰 교환 (Access/Refresh Token 수신)
    /// </summary>
    /// <remarks>
    /// 서비스 서버(:7001)가 인증 서버(:7213)로 인가 코드와 PKCE 원본키를 백채널 직통신으로 전송하여 Access Token과 Refresh Token을 발급받고 세션을 갱신합니다.
    /// </remarks>
    [HttpPost("api/auth/backchannel-token-exchange")]
    [Tags("Step 6. Back-Channel 직통신 토큰 교환 (Access/Refresh Token 수신)")]
    public async Task<IActionResult> BackchannelTokenExchange([FromBody] BackchannelExchangeDto request, CancellationToken cancellationToken)
    {
        var result = await _exchangeTokenUseCase.ExecuteAsync(request.Code, request.CodeVerifier, request.RedirectUri, HttpContext, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                message = "인증 서버와의 Back-channel 직통신 토큰 교환 실패",
                response = result.ResponseContent
            });
        }

        await IssueServiceSessionCookieAsync(result);

        return Ok(new
        {
            success = true,
            message = "서버 간 Back-channel 직통신 토큰 교환 성공 및 서비스 세션 쿠키(.NsqHomepage.ServiceSession) 발급 완료",
            sessionCookie = new
            {
                name = ".NsqHomepage.ServiceSession",
                isIssued = true,
                httpOnly = true,
                secure = true,
                sameSite = "Lax",
                expiresInMinutes = 15
            },
            sessionMemory = new
            {
                isStored = true,
                storageType = "DistributedMemoryCache (ISession)",
                storedTokens = new[] { "access_token", "id_token", "refresh_token" }
            },
            serverDirectChannel = new
            {
                caller = "ServiceServer (:7001)",
                targetIdp = "AuthServer (:7213)",
                protocol = "Direct HTTP POST",
                status = "200 OK"
            },
            user = new
            {
                sub = result.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value,
                email = result.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value,
                name = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value,
                role = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value,
                isAuthenticated = true
            },
            tokens = result.RootElement
        });
    }

    [HttpGet("api/auth/user-identity")]
    [HttpGet("api/auth/me")]
    [Tags("Step 9. 세션 쿠키 기반 사용자 신원/권한 확인 (Check User Identity)")]
    public IActionResult GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });
            var userName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "Admin";
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? HttpContext.Session.GetString("user_email");

            return Ok(new
            {
                isAuthenticated = true,
                authenticationType = User.Identity?.AuthenticationType ?? "Cookie",
                userName,
                role,
                email,
                claims
            });
        }

        var sessionSub = HttpContext.Session.GetString("user_sub");
        if (!string.IsNullOrWhiteSpace(sessionSub))
        {
            return Ok(new
            {
                isAuthenticated = true,
                authenticationType = "SessionMemory",
                userName = HttpContext.Session.GetString("user_name"),
                role = HttpContext.Session.GetString("user_role") ?? "Admin",
                email = HttpContext.Session.GetString("user_email"),
                claims = Array.Empty<object>()
            });
        }

        return Ok(new
        {
            isAuthenticated = false,
            authenticationType = (string?)null,
            userName = (string?)null,
            role = (string?)null,
            email = (string?)null,
            claims = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Refresh Token을 이용하여 Access Token 재발급 및 세션 갱신
    /// </summary>
    [HttpPost("api/auth/refresh")]
    [Tags("Step 11. 서비스 세션 로그아웃 & 토큰 갱신 (Session Management)")]
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
    /// [세션 메모리 조회] 서비스 서버 메모리 세션(ISession)에 보관된 OIDC 토큰 세트 확인
    /// </summary>
    /// <remarks>
    /// 서비스 서버의 세션 메모리에 보관된 `access_token`, `id_token`, `refresh_token` 및 유효기간을 실시간으로 확인합니다.
    /// </remarks>
    [HttpGet("api/auth/session-tokens")]
    [Tags("Step 8. 세션 메모리 보관 토큰 확인 (Inspect Session Tokens)")]
    public IActionResult GetSessionTokens()
    {
        var accessToken = HttpContext.Session.GetString("access_token");
        var idToken = HttpContext.Session.GetString("id_token");
        var refreshToken = HttpContext.Session.GetString("refresh_token");
        var tokenType = HttpContext.Session.GetString("token_type") ?? "Bearer";
        var expiresIn = HttpContext.Session.GetInt32("expires_in") ?? 900;
        var storedAt = HttpContext.Session.GetString("token_stored_at_utc");
        var sub = HttpContext.Session.GetString("user_sub");
        var email = HttpContext.Session.GetString("user_email");
        var name = HttpContext.Session.GetString("user_name");
        var role = HttpContext.Session.GetString("user_role");

        var hasSessionTokens = !string.IsNullOrWhiteSpace(accessToken);

        return Ok(new
        {
            hasSessionTokens,
            message = hasSessionTokens
                ? "서비스 서버 세션 메모리(ISession)에 OIDC 토큰 세트가 정상 보관되어 있습니다."
                : "세션 메모리에 보관된 토큰이 없습니다 (로그인 필요).",
            tokenSet = hasSessionTokens ? new
            {
                tokenType,
                accessToken,
                idToken,
                refreshToken,
                expiresIn,
                storedAtUtc = storedAt
            } : null,
            user = hasSessionTokens ? new
            {
                sub,
                email,
                name,
                role
            } : null
        });
    }

    /// <summary>
    /// [세션 종료] 서비스 세션 파기 및 전역 SSO 로그아웃 URL 반환/리다이렉트
    /// </summary>
    [HttpGet("api/auth/logout"), HttpPost("api/auth/logout")]
    [Tags("Step 11. 서비스 세션 로그아웃 & 토큰 갱신 (Session Management)")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(".NsqHomepage.ServiceSession");
        _oidcStateService.ClearSession(HttpContext);
        HttpContext.Session.Remove("access_token");
        HttpContext.Session.Remove("id_token");
        HttpContext.Session.Remove("refresh_token");
        HttpContext.Session.Remove("user_sub");
        HttpContext.Session.Remove("user_email");
        HttpContext.Session.Remove("user_name");
        HttpContext.Session.Remove("user_role");

        var targetReturnUrl = returnUrl ?? Request.Headers.Referer.ToString();
        if (string.IsNullOrWhiteSpace(targetReturnUrl))
        {
            targetReturnUrl = "http://localhost:3000/";
        }

        var authServerLogoutUrl = $"https://localhost:7213/connect/logout?post_logout_redirect_uri={Uri.EscapeDataString(targetReturnUrl)}";

        if (Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(authServerLogoutUrl);
        }

        return Ok(new
        {
            success = true,
            logoutUrl = authServerLogoutUrl,
            message = "서비스 세션 쿠키(.NsqHomepage.ServiceSession) 및 세션 메모리 토큰이 성공적으로 파기되었습니다."
        });
    }

    private async Task IssueServiceSessionCookieAsync(TokenExchangeResultDto result)
    {
        var claims = new List<Claim>(result.Claims);

        var sub = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1";
        var name = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value
                   ?? User.Identity?.Name ?? "User";
        var email = claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value
                    ?? User.FindFirstValue(ClaimTypes.Email) ?? "user@company.local";
        var role = claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value
                   ?? User.FindFirstValue(ClaimTypes.Role) ?? "Admin";

        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));

        if (!claims.Any(c => c.Type == ClaimTypes.Name))
            claims.Add(new Claim(ClaimTypes.Name, name));

        if (!claims.Any(c => c.Type == ClaimTypes.Email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        if (!claims.Any(c => c.Type == ClaimTypes.Role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var accessToken = (result.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null)
                          ?? (result.RootElement.TryGetProperty("accessToken", out var at2) ? at2.GetString() : null);
        var idToken = (result.RootElement.TryGetProperty("id_token", out var it) ? it.GetString() : null)
                      ?? (result.RootElement.TryGetProperty("idToken", out var it2) ? it2.GetString() : null);
        var refreshToken = (result.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null)
                           ?? (result.RootElement.TryGetProperty("refreshToken", out var rt2) ? rt2.GetString() : null);
        var tokenType = (result.RootElement.TryGetProperty("token_type", out var tt) ? tt.GetString() : null)
                        ?? (result.RootElement.TryGetProperty("tokenType", out var tt2) ? tt2.GetString() : null)
                        ?? "Bearer";
        var expiresIn = (result.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : (int?)null)
                        ?? (result.RootElement.TryGetProperty("expiresIn", out var ei2) ? ei2.GetInt32() : (int?)null)
                        ?? 900;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        }

        // 🌟 [핵심 요구사항] 서비스 서버 세션 메모리(ISession)에 OIDC 토큰 세트 보관
        HttpContext.Session.SetString("access_token", accessToken ?? "");
        HttpContext.Session.SetString("id_token", idToken ?? "");
        HttpContext.Session.SetString("refresh_token", refreshToken ?? "");
        HttpContext.Session.SetString("token_type", tokenType ?? "Bearer");
        HttpContext.Session.SetInt32("expires_in", expiresIn);
        HttpContext.Session.SetString("token_stored_at_utc", DateTime.UtcNow.ToString("O"));
        HttpContext.Session.SetString("user_sub", sub);
        HttpContext.Session.SetString("user_email", email);
        HttpContext.Session.SetString("user_name", name);
        HttpContext.Session.SetString("user_role", role);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            authProperties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = accessToken },
                new AuthenticationToken { Name = "id_token", Value = idToken ?? "" },
                new AuthenticationToken { Name = "refresh_token", Value = refreshToken ?? "" }
            });
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        _logger.LogInformation("서비스 서버 세션 메모리 및 세션 쿠키(.NsqHomepage.ServiceSession)에 OIDC 토큰 세트 저장 완료 - User: {Email}", email);
    }
}

public class BackchannelExchangeDto
{
    /// <summary>
    /// 인증 서버가 발급한 일회용 인가 코드 (Step 3에서 복사한 code 값)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 서비스 서버 세션에 보관된 PKCE 원본키 (생략 시 Step 1에서 세션 메모리에 저장된 codeVerifier가 자동 사용됩니다)
    /// </summary>
    public string? CodeVerifier { get; set; }

    /// <summary>
    /// 인가 요청 시 등록된 콜백 URL (기본값: https://localhost:7001/api/auth/oidc-callback)
    /// </summary>
    public string? RedirectUri { get; set; }
}

public class StartSsoResponseDto
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = "company-homepage";
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// PKCE 32바이트 원본키 (Step 8 백채널 토큰 교환 시 code_verifier로 사용)
    /// </summary>
    public string CodeVerifier { get; set; } = string.Empty;

    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = "S256";
    public string State { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid profile email roles offline_access";
    public string RedirectUri { get; set; } = "https://localhost:7001/api/auth/oidc-callback";
    public string? ReturnUrl { get; set; }
}
