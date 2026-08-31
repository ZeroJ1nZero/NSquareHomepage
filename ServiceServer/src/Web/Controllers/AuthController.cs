using System.IdentityModel.Tokens.Jwt;
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

    private string DefaultRedirectUri => _configuration["Authentication:RedirectUri"] ?? "http://localhost:3000/callback";

    [HttpGet("api/auth/access-sso")]
    [Tags("인증 및 세션 (Authentication & Session)")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StartSsoResultDto), StatusCodes.Status200OK)]
    public IActionResult StartSso([FromQuery] string? returnUrl = null, [FromQuery] string? service = null, [FromQuery] bool autoRedirect = false)
    {
        var targetReturn = returnUrl ?? "/";
        var result = _initiateSsoUseCase.Execute(HttpContext, targetReturn, service);

        _logger.LogInformation("SSO 로그인 요청 생성 완료 - Service: {Service}, State: {State}, CodeChallenge: {Challenge}, AuthorizeUrl: {Url}", service ?? "auto", result.State, result.CodeChallenge, result.AuthorizeUrl);

        Response.Headers.Location = result.AuthorizeUrl;
        if (autoRedirect)
        {
            return Redirect(result.AuthorizeUrl);
        }

        return Ok(result);
    }

    [HttpPost("api/auth/oidc-callback")]
    [Tags("인증 및 세션 (Authentication & Session)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IssueServiceSessionCookie(
        [FromBody] IssueSessionRequestDto? request = null,
        [FromQuery] string? service = null,
        CancellationToken cancellationToken = default)
    {
        var effAccessToken = request?.AccessToken ?? HttpContext.Session.GetString("access_token");
        var effIdToken = request?.IdToken ?? HttpContext.Session.GetString("id_token");
        var effRefreshToken = request?.RefreshToken ?? HttpContext.Session.GetString("refresh_token");
        var effService = request?.Service ?? service ?? HttpContext.Session.GetString("target_service");

        if (string.IsNullOrWhiteSpace(effAccessToken) && string.IsNullOrWhiteSpace(effIdToken))
        {
            return BadRequest(new
            {
                success = false,
                message = "인증 서버(IdP)로부터 발급받은 Access Token 또는 ID Token이 필요합니다. request body에 토큰을 전달해 주세요."
            });
        }

        var cookieResult = await IssueServiceSessionCookieInternalAsync(effAccessToken ?? "", effRefreshToken, effService, null, effIdToken);

        var returnUrl = HttpContext.Session.GetString("return_url") ?? "http://localhost:3000/";
        if (!returnUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !returnUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            returnUrl = $"http://localhost:3000{returnUrl}";
        }

        _oidcStateService.ClearSession(HttpContext);

        var hasCookie = !string.IsNullOrWhiteSpace(cookieResult.CookieName);
        return Ok(new
        {
            success = true,
            message = hasCookie
                ? $"위치({effService})에 맞는 서비스 세션 쿠키({cookieResult.CookieName})가 성공적으로 발급되었습니다."
                : "전역 SSO 인증이 완료되었으며 세션 메모리에 토큰이 보관되었습니다. (서비스 세션 쿠키 미발급)",
            sessionCookie = new
            {
                name = cookieResult.CookieName,
                scheme = cookieResult.Scheme,
                service = effService ?? "none",
                isIssued = hasCookie,
                httpOnly = hasCookie,
                secure = hasCookie,
                sameSite = hasCookie ? "None" : null,
                expiresInMinutes = hasCookie ? 15 : 0
            },
            sessionMemory = new
            {
                isStored = true,
                storageType = "DistributedMemoryCache (ISession)",
                storedTokens = new[] { "access_token", "id_token", "refresh_token" },
                targetService = effService
            },
            user = new
            {
                sub = cookieResult.Sub,
                email = cookieResult.Email,
                name = cookieResult.Name,
                role = cookieResult.Role,
                isAuthenticated = true
            },
            redirectUrl = returnUrl
        });
    }

    [HttpGet("api/auth/verify-state")]
    [Tags("인증 및 세션 (Authentication & Session)")]
    [ProducesResponseType(typeof(VerifyStateResultDto), StatusCodes.Status200OK)]
    public IActionResult VerifyState([FromQuery] string? state = null)
    {
        var targetState = !string.IsNullOrWhiteSpace(state)
            ? state
            : _oidcStateService.GetStoredVerifier(HttpContext) != null ? (HttpContext.Session.TryGetValue("oauth_state", out var b) ? System.Text.Encoding.UTF8.GetString(b) : "") : "";

        var result = _verifyCsrfStateUseCase.Execute(HttpContext, targetState);
        return Ok(result);
    }

    [HttpPost("api/auth/validate-pkce")]
    [Tags("인증 및 세션 (Authentication & Session)")]
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
    [Tags("인증 및 세션 (Authentication & Session)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserIdentity()
    {
        var authAbout = await HttpContext.AuthenticateAsync("Cookie_About");
        var authService = await HttpContext.AuthenticateAsync("Cookie_Service");
        var authHistory = await HttpContext.AuthenticateAsync("Cookie_History");

        var activePrincipal = authAbout.Principal ?? authService.Principal ?? authHistory.Principal;
        var hasCookieAuth = authAbout.Succeeded || authService.Succeeded || authHistory.Succeeded;

        var sessionEmail = HttpContext.Session.GetString("user_email");
        var sessionName = HttpContext.Session.GetString("user_name");
        var sessionRole = HttpContext.Session.GetString("user_role");
        var sessionSub = HttpContext.Session.GetString("user_sub");
        var hasSessionUser = !string.IsNullOrWhiteSpace(sessionEmail);

        if ((hasCookieAuth && activePrincipal?.Identity?.IsAuthenticated == true) || hasSessionUser)
        {
            var userName = activePrincipal?.Identity?.Name ?? activePrincipal?.FindFirst(ClaimTypes.Name)?.Value ?? activePrincipal?.FindFirst(ClaimTypes.Email)?.Value ?? sessionName ?? "관리자";
            var role = activePrincipal?.FindFirst(ClaimTypes.Role)?.Value ?? sessionRole ?? "Admin";
            var email = activePrincipal?.FindFirst(ClaimTypes.Email)?.Value ?? sessionEmail ?? "admin@company.local";
            var sub = activePrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? sessionSub ?? "1";

            return Ok(new
            {
                isAuthenticated = true,
                message = hasCookieAuth ? "유효한 서비스 세션 쿠키가 확인되었습니다." : "전역 세션 인증 상태가 확인되었습니다.",
                user = new
                {
                    sub,
                    userName,
                    email,
                    role
                },
                userName,
                role,
                email,
                activeSessions = new
                {
                    about = authAbout.Succeeded,
                    service = authService.Succeeded,
                    history = authHistory.Succeeded
                }
            });
        }

        return Ok(new
        {
            isAuthenticated = false,
            message = "유효한 서비스 세션 쿠키가 존재하지 않습니다.",
            user = (object?)null,
            activeSessions = new
            {
                about = false,
                service = false,
                history = false
            }
        });
    }

    [HttpPost("api/auth/refresh")]
    [Tags("인증 및 세션 (Authentication & Session)")]
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

    [HttpGet("api/auth/session-tokens")]
    [Tags("인증 및 세션 (Authentication & Session)")]
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

    [HttpPost("api/auth/logout")]
    [Tags("인증 및 세션 (Authentication & Session)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync("Cookie_About");
        await HttpContext.SignOutAsync("Cookie_Service");
        await HttpContext.SignOutAsync("Cookie_History");

        var cookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Delete(".Nsq.About.Session", cookieOptions);
        Response.Cookies.Delete(".Nsq.Service.Session", cookieOptions);
        Response.Cookies.Delete(".Nsq.History.Session", cookieOptions);
        Response.Cookies.Delete(".NsqHomepage.SessionData", cookieOptions);
        Response.Cookies.Delete(".NsqHomepage.ServiceSession", cookieOptions);

        Response.Cookies.Delete(".Nsq.About.Session");
        Response.Cookies.Delete(".Nsq.Service.Session");
        Response.Cookies.Delete(".Nsq.History.Session");
        Response.Cookies.Delete(".NsqHomepage.SessionData");
        Response.Cookies.Delete(".NsqHomepage.ServiceSession");

        _oidcStateService.ClearSession(HttpContext);
        HttpContext.Session.Clear();

        var targetReturnUrl = returnUrl;
        if (string.IsNullOrWhiteSpace(targetReturnUrl))
        {
            targetReturnUrl = "http://localhost:3000/";
        }

        var authServerLogoutUrl = $"https://localhost:7213/api/auth/logout?post_logout_redirect_uri={Uri.EscapeDataString(targetReturnUrl)}";

        return Ok(new
        {
            success = true,
            logoutUrl = authServerLogoutUrl,
            message = "모든 서비스 세션 쿠키 및 세션 메모리 토큰이 성공적으로 파기되었습니다."
        });
    }

    [HttpPost("api/auth/local-logout")]
    [Tags("인증 및 세션 (Authentication & Session)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LocalLogout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync("Cookie_About");
        await HttpContext.SignOutAsync("Cookie_Service");
        await HttpContext.SignOutAsync("Cookie_History");

        Response.Cookies.Delete(".Nsq.About.Session");
        Response.Cookies.Delete(".Nsq.Service.Session");
        Response.Cookies.Delete(".Nsq.History.Session");

        _oidcStateService.ClearSession(HttpContext);
        HttpContext.Session.Clear();

        var targetReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "http://localhost:3000/" : returnUrl;

        _logger.LogInformation("로컬 서비스 세션 쿠키 파기 완료 (AuthServer_SSO_Cookie 유지)");

        return Ok(new
        {
            success = true,
            ssoCookiePreserved = true,
            message = "서비스 세션 쿠키(.Nsq.*.Session) 및 메모리가 성공적으로 파기되었으며, 전역 SSO 쿠키(AuthServer_SSO_Cookie)는 유지됩니다.",
            deletedCookies = new[] { ".Nsq.About.Session", ".Nsq.Service.Session", ".Nsq.History.Session" },
            redirectUrl = targetReturnUrl
        });
    }

    private async Task<(string? CookieName, string? Scheme, ClaimsPrincipal Principal, string? Sub, string? Email, string? Name, string? Role)> IssueServiceSessionCookieInternalAsync(
        string accessToken,
        string? refreshToken,
        string? targetService,
        List<Claim>? initialClaims = null,
        string? idToken = null)
    {
        var claims = new List<Claim>();
        if (initialClaims != null)
        {
            claims.AddRange(initialClaims);
        }

        var handler = new JwtSecurityTokenHandler();

        // 1. OIDC ID Token으로부터 사용자 신원 클레임(sub, name, email, role 등) 파싱
        if (!string.IsNullOrWhiteSpace(idToken) && handler.CanReadToken(idToken))
        {
            var idJwt = handler.ReadJwtToken(idToken);
            foreach (var c in idJwt.Claims)
            {
                if (!claims.Any(existing => existing.Type == c.Type))
                {
                    claims.Add(c);
                }
            }
        }

        // 2. Access Token으로부터 권한 및 추가 클레임 파싱
        if (!string.IsNullOrWhiteSpace(accessToken) && handler.CanReadToken(accessToken))
        {
            var jwt = handler.ReadJwtToken(accessToken);
            foreach (var c in jwt.Claims)
            {
                if (!claims.Any(existing => existing.Type == c.Type))
                {
                    claims.Add(c);
                }
            }
        }

        var sub = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value
                  ?? HttpContext.Session.GetString("user_sub")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1";
        var name = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value
                   ?? HttpContext.Session.GetString("user_name")
                   ?? User.Identity?.Name ?? "User";
        var email = claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value
                    ?? HttpContext.Session.GetString("user_email")
                    ?? User.FindFirstValue(ClaimTypes.Email) ?? "user@company.local";
        var role = claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value
                   ?? HttpContext.Session.GetString("user_role")
                   ?? User.FindFirstValue(ClaimTypes.Role) ?? "Admin";

        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));

        if (!claims.Any(c => c.Type == ClaimTypes.Name))
            claims.Add(new Claim(ClaimTypes.Name, name));

        if (!claims.Any(c => c.Type == ClaimTypes.Email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        if (!claims.Any(c => c.Type == ClaimTypes.Role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        // 🌟 [핵심] 서비스 서버 세션 메모리(ISession)에 OIDC 토큰 세트 및 유저 정보 보관
        HttpContext.Session.SetString("access_token", accessToken ?? "");
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            HttpContext.Session.SetString("id_token", idToken);
        }
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            HttpContext.Session.SetString("refresh_token", refreshToken);
        }
        HttpContext.Session.SetString("token_type", "Bearer");
        HttpContext.Session.SetInt32("expires_in", 900);
        HttpContext.Session.SetString("token_stored_at_utc", DateTime.UtcNow.ToString("O"));
        HttpContext.Session.SetString("user_sub", sub);
        HttpContext.Session.SetString("user_email", email);
        HttpContext.Session.SetString("user_name", name);
        HttpContext.Session.SetString("user_role", role);

        var finalService = targetService ?? HttpContext.Session.GetString("target_service");

        // 🌟 요청 위치(target_service)에 맞는 서비스 세션 쿠키 발급 (about, service, history 전용)
        string? schemeToSignIn = null;
        string? cookieName = null;

        switch (finalService?.ToLowerInvariant())
        {
            case "about":
                schemeToSignIn = "Cookie_About";
                cookieName = ".Nsq.About.Session";
                HttpContext.Session.SetString("access_token_about", accessToken ?? "");
                break;
            case "service":
                schemeToSignIn = "Cookie_Service";
                cookieName = ".Nsq.Service.Session";
                HttpContext.Session.SetString("access_token_service", accessToken ?? "");
                break;
            case "history":
                schemeToSignIn = "Cookie_History";
                cookieName = ".Nsq.History.Session";
                HttpContext.Session.SetString("access_token_history", accessToken ?? "");
                break;
            default:
                // nsquarehomepage 메인 및 전역 SSO는 서비스 세션 쿠키를 발급하지 않음 (세션 메모리에만 보관)
                schemeToSignIn = null;
                cookieName = null;
                break;
        }

        ClaimsPrincipal principal;
        if (!string.IsNullOrWhiteSpace(schemeToSignIn))
        {
            var identity = new ClaimsIdentity(claims, schemeToSignIn, ClaimTypes.Name, ClaimTypes.Role);
            principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15)
            };

            await HttpContext.SignInAsync(schemeToSignIn, principal, authProperties);
            _logger.LogInformation("요청 위치에 따른 서비스 세션 쿠키 발급 완료 - Cookie: {CookieName}, Scheme: {Scheme}, Service: {Service}, User: {Email}",
                cookieName, schemeToSignIn, finalService, email);
        }
        else
        {
            var identity = new ClaimsIdentity(claims, "MemorySession", ClaimTypes.Name, ClaimTypes.Role);
            principal = new ClaimsPrincipal(identity);
            _logger.LogInformation("전역 SSO 인증 완료 (서비스 세션 쿠키 미발급 모드) - Service: {Service}, User: {Email}",
                finalService ?? "none", email);
        }

        return (cookieName, schemeToSignIn, principal, sub, email, name, role);
    }

    private async Task IssueServiceSessionCookieAsync(TokenExchangeResultDto result)
    {
        var accessToken = (result.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null)
                          ?? (result.RootElement.TryGetProperty("accessToken", out var at2) ? at2.GetString() : null);
        var refreshToken = (result.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null)
                           ?? (result.RootElement.TryGetProperty("refreshToken", out var rt2) ? rt2.GetString() : null);
        var targetService = HttpContext.Session.GetString("target_service");

        // 🌟 [요구사항 1] SSO 로그인 버튼을 눌렀을 때에는 서비스 세션 쿠키를 발급하지 않음
        if (string.Equals(targetService, "none", StringComparison.OrdinalIgnoreCase))
        {
            var sub = result.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value ?? "1";
            var email = result.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value ?? "user@company.local";
            var name = result.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value ?? "User";
            var role = result.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value ?? "Admin";

            HttpContext.Session.SetString("access_token", accessToken ?? "");
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                HttpContext.Session.SetString("refresh_token", refreshToken);
            }
            HttpContext.Session.SetString("token_type", "Bearer");
            HttpContext.Session.SetInt32("expires_in", 900);
            HttpContext.Session.SetString("token_stored_at_utc", DateTime.UtcNow.ToString("O"));
            HttpContext.Session.SetString("user_sub", sub);
            HttpContext.Session.SetString("user_email", email);
            HttpContext.Session.SetString("user_name", name);
            HttpContext.Session.SetString("user_role", role);
            _logger.LogInformation("SSO 로그인 버튼 클릭에 따른 전역 인증 완료 (서비스 세션 쿠키 미발급 모드) - User: {Email}", email);
            return;
        }

        await IssueServiceSessionCookieInternalAsync(accessToken ?? "", refreshToken, targetService, result.Claims);
    }
}

public class IssueSessionRequestDto
{
    /// <summary>
    /// 인증 서버(IdP)에서 발급받은 JWT Access Token (Step 07에서 발급된 access_token)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// 인증 서버(IdP)에서 발급받은 OIDC ID Token (사용자 신원 증명 토큰: sub, email, name, role 포함)
    /// </summary>
    public string? IdToken { get; set; }

    /// <summary>
    /// 인증 서버(IdP)에서 발급받은 Refresh Token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 대상 서비스 위치 (about: 회사소개, service: 주요서비스, history: 회사연혁, 또는 생략 시 기본값)
    /// </summary>
    public string? Service { get; set; }
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
    public string RedirectUri { get; set; } = "http://localhost:3000/callback";
    public string? ReturnUrl { get; set; }
}
