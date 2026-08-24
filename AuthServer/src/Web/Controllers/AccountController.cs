using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Web.Controllers;

/// <summary>
/// OIDC 중앙 인증 서버(IdP)의 사용자 로그인, SSO 쿠키 및 인가 코드(PKCE 해시) 관리 컨트롤러
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AccountController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    ILoginAuditor auditor,
    ILogger<AccountController> logger) : ControllerBase
{
    /// <summary>
    /// [SSO 로그인] 아이디/비밀번호 검증 및 암호화된 SSO 쿠키(AuthServer_SSO_Cookie) 발급
    /// </summary>
    /// <remarks>
    /// 제출된 계정(Email)과 비밀번호(Password)를 MariaDB의 PBKDF2 해시와 실시간 대조하여 검증합니다.
    /// 
    /// **[인증 완료 후 동작]**
    /// 1. DataProtection으로 암호화된 **`AuthServer_SSO_Cookie`**를 HTTP 응답 헤더(`Set-Cookie`)에 동봉하여 반환합니다.
    /// 2. 클라이언트는 수신한 쿠키를 브라우저에 저장하고, 반환된 **`redirectUrl`**(인가 엔드포인트 `/connect/authorize?...` 또는 메인 홈)로 이동합니다.
    /// 3. 이후 타 서비스(소개, 서비스, 연혁 등) 접근 시 이 SSO 쿠키가 자동 첨부되어 재로그인 없이 즉시 인가됩니다.
    /// </remarks>
    /// <param name="request">로그인 요청 정보 (이메일, 비밀번호, 리다이렉트 URL)</param>
    /// <param name="ct">취소 토큰</param>
    /// <response code="200">인증 성공 및 Set-Cookie 헤더에 암호화된 SSO 쿠키 동봉</response>
    /// <response code="400">아이디 또는 비밀번호 불일치</response>
    [HttpPost("login")]
    [Tags("Step 2. SSO 인증 & 암호화 쿠키 발급 (SSO Authentication)")]
    [ProducesResponseType(typeof(SsoLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] SsoLoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "이메일과 비밀번호를 모두 입력해 주세요."
            });
        }

        var email = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        var succeeded = user is not null &&
            !string.IsNullOrEmpty(request.Password) &&
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) is not PasswordVerificationResult.Failed;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await auditor.RecordAsync(email, ip, succeeded);

        if (!succeeded || user is null)
        {
            logger.LogWarning("로그인 실패 - Email: {Email}, IP: {IP}", email, ip);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "아이디 또는 비밀번호가 올바르지 않습니다."
            });
        }

        // Claims 구성 (Sub, Name, Email, Role)
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        // 🌟 ASP.NET Core DataProtection으로 암호화된 SSO 쿠키 발급 (Set-Cookie 헤더 자동 첨부)
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authProperties);

        logger.LogInformation("SSO 로그인 성공 및 암호화 쿠키 발급 완료 - User: {User}({Role}), IP: {IP}", user.UserName, user.Role, ip);

        var targetRedirectUrl = request.ReturnUrl;
        if (!string.IsNullOrWhiteSpace(targetRedirectUrl))
        {
            targetRedirectUrl = System.Text.RegularExpressions.Regex.Replace(
                targetRedirectUrl, @"([?&])prompt=[^&]*(&|$)", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .TrimEnd('?', '&');
        }
        else
        {
            targetRedirectUrl = "/";
        }

        return Ok(new SsoLoginResponseDto
        {
            Success = true,
            Message = "사용자 인증 성공 및 암호화된 SSO 쿠키(AuthServer_SSO_Cookie)가 Set-Cookie 헤더에 동봉되었습니다.",
            User = new UserSummaryDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                Role = user.Role,
                RoleName = user.Role.ToString()
            },
            RedirectUrl = targetRedirectUrl,
            CookieName = "AuthServer_SSO_Cookie",
            CookieEncrypted = true,
            ExpiresInDays = 14
        });
    }

    /// <summary>
    /// [SSO 상태 확인] 현재 SSO 쿠키(AuthServer_SSO_Cookie) 인증 상태 및 복호화된 Claims 확인
    /// </summary>
    /// <remarks>
    /// 클라이언트 브라우저가 전송한 SSO 쿠키를 복호화하여 현재 로그인된 유저의 정보와 권한을 반환합니다.
    /// </remarks>
    [HttpGet("status")]
    [Tags("Step 2. SSO 인증 & 암호화 쿠키 발급 (SSO Authentication)")]
    [ProducesResponseType(typeof(SsoStatusResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            return Ok(new SsoStatusResponseDto
            {
                IsAuthenticated = false,
                Message = "SSO 쿠키가 존재하지 않거나 만료되었습니다 (비로그인 상태)."
            });
        }

        var principal = authResult.Principal;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        var role = principal.FindFirstValue(ClaimTypes.Role);

        return Ok(new SsoStatusResponseDto
        {
            IsAuthenticated = true,
            Message = "유효한 SSO 쿠키(AuthServer_SSO_Cookie)를 보유하고 있습니다.",
            UserId = userId,
            Email = email,
            UserName = name,
            Role = role,
            Claims = principal.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    /// <summary>
    /// [SSO 로그아웃] SSO 세션 종료 및 SSO 쿠키 파기
    /// </summary>
    /// <remarks>
    /// 인증 서버의 SSO 쿠키를 파기하여 모든 연동 마이크로서비스에서의 싱글 사인온 세션을 만료시킵니다.
    /// </remarks>
    [HttpPost("logout")]
    [Tags("Step 11. SSO 전역 세션 로그아웃 (Global SSO Logout)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new
        {
            success = true,
            message = "SSO 세션이 종료되었으며 AuthServer_SSO_Cookie가 파기되었습니다."
        });
    }

    /// <summary>
    /// [OIDC 인가 코드 발급] 인가 코드(authorization_code) 생성 및 DB 저장 (authorization_code + code_challenge SHA-256 해시 연동)
    /// </summary>
    /// <remarks>
    /// 클라이언트가 전달한 PKCE `code_challenge`, `state`, `client_id`, `redirect_uri`, `scope`를 검증하고,
    /// 1. 암호학적 난수 **인가 코드(`authorization_code`)**를 생성합니다.
    /// 2. **인가 코드 원문/해시(SHA-256)**와 **`code_challenge` 원문/해시(SHA-256)**를 MariaDB `IssuedAuthorizationCodes` 테이블에 함께 저장합니다.
    /// 3. 클라이언트에게 **인가 코드(`authorization_code`), `state`, `iss`, `redirect_url` 및 파라미터 일체**를 반환/전달합니다.
    /// </remarks>
    /// <param name="request">인가 코드 요청 파라미터 (client_id, redirect_uri, code_challenge, state 등)</param>
    /// <param name="ct">취소 토큰</param>
    [HttpPost("authorize-code")]
    [Tags("Step 3. OIDC 인가 코드 발급 & DB 해시 저장 (Issue Authorization Code)")]
    [ProducesResponseType(typeof(GenerateAuthCodeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAuthorizationCode([FromBody] GenerateAuthCodeRequestDto request, CancellationToken ct)
    {
        // 🌟 AuthorizeUrl이 입력된 경우 URL 쿼리 파라미터에서 code_challenge, state, client_id 등을 자동 추출
        var codeChallenge = request.CodeChallenge;
        var state = request.State;
        var clientId = request.ClientId;
        var redirectUri = request.RedirectUri;
        var scope = request.Scope;
        var codeChallengeMethod = request.CodeChallengeMethod;

        if (!string.IsNullOrWhiteSpace(request.AuthorizeUrl))
        {
            try
            {
                var uri = new Uri(request.AuthorizeUrl);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (string.IsNullOrWhiteSpace(codeChallenge) && query.TryGetValue("code_challenge", out var qChallenge))
                    codeChallenge = qChallenge.ToString();
                if (string.IsNullOrWhiteSpace(state) && query.TryGetValue("state", out var qState))
                    state = qState.ToString();
                if (string.IsNullOrWhiteSpace(clientId) && query.TryGetValue("client_id", out var qClient))
                    clientId = qClient.ToString();
                if (string.IsNullOrWhiteSpace(redirectUri) && query.TryGetValue("redirect_uri", out var qRedirect))
                    redirectUri = qRedirect.ToString();
                if (string.IsNullOrWhiteSpace(scope) && query.TryGetValue("scope", out var qScope))
                    scope = qScope.ToString();
                if (string.IsNullOrWhiteSpace(codeChallengeMethod) && query.TryGetValue("code_challenge_method", out var qMethod))
                    codeChallengeMethod = qMethod.ToString();
            }
            catch
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.AuthorizeUrl);
                if (string.IsNullOrWhiteSpace(codeChallenge) && query.TryGetValue("code_challenge", out var qChallenge))
                    codeChallenge = qChallenge.ToString();
                if (string.IsNullOrWhiteSpace(state) && query.TryGetValue("state", out var qState))
                    state = qState.ToString();
                if (string.IsNullOrWhiteSpace(clientId) && query.TryGetValue("client_id", out var qClient))
                    clientId = qClient.ToString();
                if (string.IsNullOrWhiteSpace(redirectUri) && query.TryGetValue("redirect_uri", out var qRedirect))
                    redirectUri = qRedirect.ToString();
                if (string.IsNullOrWhiteSpace(scope) && query.TryGetValue("scope", out var qScope))
                    scope = qScope.ToString();
            }
        }

        clientId = string.IsNullOrWhiteSpace(clientId) ? "company-homepage" : clientId;
        redirectUri = string.IsNullOrWhiteSpace(redirectUri) ? "https://localhost:7001/api/auth/oidc-callback" : redirectUri;
        codeChallengeMethod = string.IsNullOrWhiteSpace(codeChallengeMethod) ? "S256" : codeChallengeMethod;
        scope = string.IsNullOrWhiteSpace(scope) ? "openid profile email roles offline_access" : scope;
        state = state ?? string.Empty;

        if (string.IsNullOrWhiteSpace(codeChallenge))
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "PKCE code_challenge를 찾을 수 없습니다. (Step 1의 authorizeUrl을 그대로 입력하거나 codeChallenge를 입력해 주세요)"
            });
        }

        // 현재 세션 또는 요청된 이메일로부터 사용자 확인
        var email = request.UserEmail?.Trim() ?? "test@company.local";
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authResult.Succeeded && authResult.Principal is not null)
        {
            email = authResult.Principal.FindFirstValue(ClaimTypes.Email) ?? email;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == "test@company.local", ct);
        }

        if (user is null)
        {
            return BadRequest(new ErrorResponseDto { Success = false, Message = "유효한 사용자 계정을 찾을 수 없습니다." });
        }

        // 1. 암호학적 32바이트 인가 코드(authorization_code) 생성 (Base64Url)
        var codeBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(codeBytes);
        }
        var authorizationCode = Base64UrlEncoder.Encode(codeBytes);

        // 2. 인가 코드 SHA-256 해시 계산
        var authorizationCodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationCode))).ToLowerInvariant();

        // 3. PKCE code_challenge SHA-256 해시 계산
        var challengeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codeChallenge))).ToLowerInvariant();

        // 4. 🌟 Zero-Trust DB 무결성 결합 해시 계산: AuthorizationCode와 CodeChallenge를 한 묶음으로 결합하여 해시
        var combinedBindingHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{authorizationCode}:{codeChallenge}"))).ToLowerInvariant();

        // 5. MariaDB IssuedAuthorizationCodes 테이블에 인가코드와 code_challenge, 결합 해시를 함께 저장
        var authRecord = new IssuedAuthorizationCode
        {
            AuthorizationCode = authorizationCode,
            AuthorizationCodeHash = authorizationCodeHash,
            CodeChallenge = codeChallenge,
            CodeChallengeHash = challengeHash,
            CombinedBindingHash = combinedBindingHash,
            CodeChallengeMethod = codeChallengeMethod,
            ClientId = clientId,
            RedirectUri = redirectUri,
            Subject = user.Id.ToString(),
            UserEmail = user.Email,
            State = state,
            Scope = scope,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IsRedeemed = false
        };

        db.IssuedAuthorizationCodes.Add(authRecord);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("MariaDB에 인가 코드(authorization_code) 및 PKCE 결합 해시 저장 완료 - AuthCodeHash: {CodeHash}, CombinedBindingHash: {BindingHash}, User: {User}",
            authorizationCodeHash, combinedBindingHash, user.Email);

        // 6. 클라이언트로 전달할 리다이렉트 URL 구성
        var issuer = "https://localhost:7213/";
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(authorizationCode)}",
            $"authorization_code={Uri.EscapeDataString(authorizationCode)}",
            $"state={Uri.EscapeDataString(request.State ?? string.Empty)}",
            $"iss={Uri.EscapeDataString(issuer)}"
        };
        var separator = authRecord.RedirectUri.Contains('?') ? "&" : "?";
        var redirectUrl = $"{authRecord.RedirectUri}{separator}{string.Join("&", queryParams)}";

        return Ok(new GenerateAuthCodeResponseDto
        {
            Success = true,
            Message = "인가 코드(authorization_code)가 성공적으로 생성되었으며, MariaDB에 [AuthorizationCode + CodeChallenge] 결합 해시가 안전하게 저장되었습니다.",
            AuthorizationCode = authorizationCode,
            State = request.State ?? string.Empty,
            Issuer = issuer,
            RedirectUrl = redirectUrl,
            DatabaseRecord = new AuthCodeDbRecordDto
            {
                Id = authRecord.Id,
                AuthorizationCodeHash = authorizationCodeHash,
                CodeChallenge = authRecord.CodeChallenge,
                CodeChallengeHash = challengeHash,
                CombinedBindingHash = combinedBindingHash,
                CodeChallengeMethod = authRecord.CodeChallengeMethod,
                ClientId = authRecord.ClientId,
                UserEmail = authRecord.UserEmail,
                Subject = authRecord.Subject,
                CreatedAtUtc = authRecord.CreatedAtUtc,
                ExpiresAtUtc = authRecord.ExpiresAtUtc,
                LifetimeSeconds = 300
            },
            ClientParameters = new
            {
                authorization_code = authorizationCode,
                code = authorizationCode,
                state = request.State,
                iss = issuer,
                client_id = authRecord.ClientId,
                code_challenge = authRecord.CodeChallenge,
                combined_binding_hash = combinedBindingHash,
                code_challenge_method = authRecord.CodeChallengeMethod,
                redirect_uri = authRecord.RedirectUri,
                scope = authRecord.Scope
            }
        });
    }

    /// <summary>
    /// [DB 저장 내역 조회] MariaDB에 저장된 인가 코드 및 PKCE Code Challenge 해시 목록 조회
    /// </summary>
    /// <remarks>
    /// MariaDB `IssuedAuthorizationCodes` 테이블에 기록된 인가 코드 발급 및 해시 저장 내역을 실시간으로 확인합니다.
    /// </remarks>
    [HttpGet("authorization-codes")]
    [Tags("Step 4. MariaDB 저장 내역 및 1회용 코드 상태 확인 (Inspect Database Codes)")]
    [ProducesResponseType(typeof(List<IssuedAuthorizationCode>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssuedAuthorizationCodes(CancellationToken ct)
    {
        var records = await db.IssuedAuthorizationCodes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        return Ok(records);
    }

    /// <summary>
    /// [서버 직통신] 인가 코드(authorization_code) + PKCE 원본키(code_verifier) Back-Channel 토큰 교환
    /// </summary>
    /// <remarks>
    /// 서비스 서버(ServiceServer :7001)로부터 인가 코드(`authorization_code`)와 PKCE 원본키(`code_verifier`)를 HTTP 백채널 직통신으로 전달받아 검증합니다.
    /// 
    /// **[서버 간 직통신 검증 파이프라인]**
    /// 1. **PKCE S256 검증**: `Base64Url(SHA256(code_verifier))` 해시를 계산하여 MariaDB에 저장된 `code_challenge`와 일치하는지 대조합니다.
    /// 2. **인가 코드 유효성 확인**: 만료시간(5분) 초과 여부 및 이미 사용된 코드(`IsRedeemed`) 여부를 검증합니다.
    /// 3. **토큰 발급 및 사용 처리**: 유효성 통과 시 `IsRedeemed = true`로 변경하고, 서명된 JWT `access_token` 및 사용자 정보를 응답합니다.
    /// </remarks>
    /// <param name="request">토큰 교환 요청 (authorization_code, code_verifier, client_id, redirect_uri)</param>
    /// <param name="ct">취소 토큰</param>
    [HttpPost("token-exchange")]
    [Tags("Step 6. Back-Channel 토큰 교환 & OIDC 토큰 세트 발급 (Direct Token Exchange)")]
    [ProducesResponseType(typeof(BackchannelTokenExchangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TokenExchange([FromBody] BackchannelTokenExchangeRequestDto request, CancellationToken ct)
    {
        var authorizationCode = !string.IsNullOrWhiteSpace(request.AuthorizationCode)
            ? request.AuthorizationCode
            : request.Code;

        if (string.IsNullOrWhiteSpace(authorizationCode) || string.IsNullOrWhiteSpace(request.CodeVerifier))
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "인가 코드(authorization_code)와 PKCE 원본키(code_verifier)는 필수 값입니다."
            });
        }

        var authorizationCodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationCode))).ToLowerInvariant();
        var record = await db.IssuedAuthorizationCodes
            .FirstOrDefaultAsync(x => x.AuthorizationCode == authorizationCode || x.AuthorizationCodeHash == authorizationCodeHash, ct);

        if (record is null)
        {
            logger.LogWarning("토큰 교환 실패 - 존재하지 않는 인가 코드(authorization_code): {AuthorizationCode}", authorizationCode);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "유효하지 않거나 존재하지 않는 인가 코드(authorization_code)입니다."
            });
        }

        if (record.IsRedeemed)
        {
            logger.LogWarning("토큰 교환 실패 - 이미 사용된 인가 코드 (ID: {Id})", record.Id);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "이미 사용(교환) 완료된 인가 코드(authorization_code)입니다. 보안을 위해 재사용할 수 없습니다."
            });
        }

        if (record.ExpiresAtUtc < DateTime.UtcNow)
        {
            logger.LogWarning("토큰 교환 실패 - 인가 코드 만료 (ID: {Id}, 만료일시: {Exp})", record.Id, record.ExpiresAtUtc);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "인가 코드(authorization_code)가 만료되었습니다. 다시 인증을 요청해 주세요."
            });
        }

        // PKCE S256 검증: Base64Url(SHA256(code_verifier)) == record.CodeChallenge
        using var sha256 = SHA256.Create();
        var calculatedChallenge = Base64UrlEncoder.Encode(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.CodeVerifier)));

        if (!string.Equals(calculatedChallenge, record.CodeChallenge, StringComparison.Ordinal))
        {
            logger.LogWarning("PKCE 검증 실패 - 계산된 Challenge: {Calc}, 저장된 Challenge: {Stored}", calculatedChallenge, record.CodeChallenge);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "PKCE 보안 검증 실패: 전달된 code_verifier의 S256 해시가 인가 시 제출된 code_challenge와 일치하지 않습니다."
            });
        }

        // 🌟 Zero-Trust DB 무결성 결합 해시 검증: AuthorizationCode와 CodeChallenge의 한 묶음 결합 해시 대조
        var calculatedBindingHash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes($"{authorizationCode}:{calculatedChallenge}"))).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(record.CombinedBindingHash) &&
            !string.Equals(record.CombinedBindingHash, calculatedBindingHash, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogCritical("🚨 [Zero-Trust DB 무결성 위반 감지] AuthorizationCode와 CodeChallenge의 결합 해시가 불일치합니다. DB 변조 차단!");
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "DB 무결성 검증 실패: 인가 코드(authorization_code)와 PKCE CodeChallenge의 결합 해시가 일치하지 않습니다 (DB 변조 감지)."
            });
        }

        // 인가 코드 사용 완료 처리
        record.IsRedeemed = true;
        record.RedeemedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // 사용자 조회
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == record.UserEmail, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == record.Subject, ct);

        var roleName = user?.Role.ToString() ?? "Admin";
        var userName = user?.UserName ?? "테스트 사용자";
        var email = user?.Email ?? record.UserEmail;
        var sub = user?.Id.ToString() ?? record.Subject;

        // 1. JWT Access Token 생성 (ResourceServer와 호환)
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKeyForDevelopmentTesting1234567890!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessClaims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, sub),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, userName),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, roleName),
            new("role", roleName),
            new("scope", record.Scope)
        };

        var accessDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(accessClaims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://localhost:7213/",
            Audience = "company-homepage",
            SigningCredentials = creds
        };
        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(accessDescriptor));

        // 2. OIDC ID Token 생성 (사용자 신원 증명)
        var idClaims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, sub),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, userName),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, roleName),
            new("role", roleName)
        };

        var idDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(idClaims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://localhost:7213/",
            Audience = "company-homepage",
            SigningCredentials = creds
        };
        var idToken = tokenHandler.WriteToken(tokenHandler.CreateToken(idDescriptor));

        // 3. Refresh Token 생성 (토큰 갱신용)
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        logger.LogInformation("서버 간 Back-channel 직통신 OIDC 토큰 세트 발급 성공 - User: {User}({Role}), CodeId: {CodeId}", email, roleName, record.Id);

        return Ok(new BackchannelTokenExchangeResponseDto
        {
            Success = true,
            Message = "서버 간 Back-channel 직통신 검증 완료: 인가 코드(authorization_code) 및 PKCE code_verifier가 성공적으로 검증되어 OIDC 토큰 세트(Access/ID/Refresh Token)가 발급되었습니다.",
            TokenType = "Bearer",
            AccessToken = accessToken,
            IdToken = idToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900,
            Scope = record.Scope,
            User = new
            {
                sub,
                email,
                name = userName,
                role = roleName
            },
            PkceValidation = new
            {
                codeVerifier = request.CodeVerifier,
                calculatedChallenge,
                storedChallenge = record.CodeChallenge,
                matched = true
            },
            AuthorizationCodeValidation = new
            {
                authorization_code = authorizationCode,
                code = authorizationCode,
                isRedeemed = true,
                redeemedAtUtc = record.RedeemedAtUtc,
                previouslyUsed = false
            },
            ServerDirectChannel = new
            {
                caller = "ServiceServer (:7001)",
                receiver = "AuthServer (:7213)",
                endpoint = "/api/auth/token-exchange",
                protocol = "Direct Back-Channel HTTP POST",
                status = "200 OK"
            }
        });
    }
}

public class SsoLoginRequestDto
{
    /// <summary>
    /// 계정 이메일 (예: test@company.local)
    /// </summary>
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식이어야 합니다.")]
    public string Email { get; set; } = "test@company.local";

    /// <summary>
    /// 계정 비밀번호 (예: Test1234!)
    /// </summary>
    [Required(ErrorMessage = "비밀번호는 필수입니다.")]
    public string Password { get; set; } = "Test1234!";

    /// <summary>
    /// 인증 성공 후 이동할 리다이렉트 주소 (예: /connect/authorize?... 또는 /)
    /// </summary>
    public string? ReturnUrl { get; set; } = "/";
}

public class SsoLoginResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserSummaryDto? User { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
    public string CookieName { get; set; } = "AuthServer_SSO_Cookie";
    public bool CookieEncrypted { get; set; } = true;
    public int ExpiresInDays { get; set; } = 14;
}

public class SsoStatusResponseDto
{
    public bool IsAuthenticated { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? Role { get; set; }
    public IEnumerable<object>? Claims { get; set; }
}

public class GenerateAuthCodeRequestDto
{
    /// <summary>
    /// Step 1에서 발급받은 authorizeUrl 전체 문자열 (입력 시 내부의 code_challenge, state, client_id 등이 자동 파싱됩니다)
    /// </summary>
    public string? AuthorizeUrl { get; set; }

    /// <summary>
    /// PKCE S256 Code Challenge 해시키 (authorizeUrl 입력 시 자동 추출되므로 생략 가능)
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// CSRF 방어용 state 값 (authorizeUrl 입력 시 자동 추출되므로 생략 가능)
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// 서비스 식별자 (기본값: company-homepage)
    /// </summary>
    public string? ClientId { get; set; } = "company-homepage";

    /// <summary>
    /// 인가 완료 후 복귀할 콜백 URL (기본값: https://localhost:7001/api/auth/oidc-callback)
    /// </summary>
    public string? RedirectUri { get; set; } = "https://localhost:7001/api/auth/oidc-callback";

    /// <summary>
    /// PKCE 해시 방식 (기본값: S256)
    /// </summary>
    public string? CodeChallengeMethod { get; set; } = "S256";

    /// <summary>
    /// 요청 권한 범위 (Scopes, 기본값: openid profile email roles offline_access)
    /// </summary>
    public string? Scope { get; set; } = "openid profile email roles offline_access";

    /// <summary>
    /// 인증할 사용자 이메일 (기본값: 로그인된 세션 유저 또는 test@company.local)
    /// </summary>
    public string? UserEmail { get; set; } = "test@company.local";
}

public class GenerateAuthCodeResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 발급된 일회용 인가 코드 (authorization_code)
    /// </summary>
    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>
    /// (하위 호환용) 인가 코드
    /// </summary>
    public string Code => AuthorizationCode;

    public string State { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public AuthCodeDbRecordDto? DatabaseRecord { get; set; }
    public object? ClientParameters { get; set; }
}

public class AuthCodeDbRecordDto
{
    public long Id { get; set; }
    public string AuthorizationCodeHash { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeHash { get; set; } = string.Empty;
    public string CombinedBindingHash { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int LifetimeSeconds { get; set; }
}

public class BackchannelTokenExchangeRequestDto
{
    /// <summary>
    /// OAuth 인가 방식 (기본값: authorization_code)
    /// </summary>
    public string GrantType { get; set; } = "authorization_code";

    /// <summary>
    /// 서비스 식별자 (기본값: company-homepage)
    /// </summary>
    public string ClientId { get; set; } = "company-homepage";

    /// <summary>
    /// 인증 서버가 발급한 일회용 인가 코드 (Step 3에서 발급된 authorization_code)
    /// </summary>
    [Required(ErrorMessage = "authorization_code는 필수입니다.")]
    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>
    /// (하위 호환용 code 파라미터 지원)
    /// </summary>
    public string? Code
    {
        get => AuthorizationCode;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(AuthorizationCode))
            {
                AuthorizationCode = value;
            }
        }
    }

    /// <summary>
    /// 서비스 서버 세션에 보관된 PKCE 원본키 (Step 1의 codeVerifier)
    /// </summary>
    [Required(ErrorMessage = "code_verifier는 필수입니다.")]
    [JsonPropertyName("code_verifier")]
    public string CodeVerifier { get; set; } = string.Empty;

    /// <summary>
    /// 인가 요청 시 등록된 콜백 URL (기본값: https://localhost:7001/api/auth/oidc-callback)
    /// </summary>
    public string RedirectUri { get; set; } = "https://localhost:7001/api/auth/oidc-callback";
}

public class BackchannelTokenExchangeResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public string AccessToken { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } = 900;
    public string Scope { get; set; } = string.Empty;
    public object? User { get; set; }
    public object? PkceValidation { get; set; }
    public object? AuthorizationCodeValidation { get; set; }
    public object? ServerDirectChannel { get; set; }
}
