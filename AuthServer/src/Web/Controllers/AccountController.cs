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
using OpenIddict.Abstractions;

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
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpPost("login")]
    [Tags("인증 및 SSO (Authentication & SSO)")]
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
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
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

        logger.LogInformation("SSO 로그인 성공 및 암호화 쿠키 발급 완료 - User: {User}({Role}), IP: {IP}", user.DisplayName, user.Role, ip);

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
                DisplayName = user.DisplayName,
                Role = user.Role,
                RoleName = user.Role.ToString()
            },
            RedirectUrl = targetRedirectUrl,
            CookieName = "AuthServer_SSO_Cookie",
            CookieEncrypted = true,
            ExpiresInDays = 14
        });
    }

    [HttpGet("status")]
    [Tags("인증 및 SSO (Authentication & SSO)")]
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
            DisplayName = name,
            Role = role,
            Claims = principal.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    [HttpGet("logout"), HttpPost("logout")]
    [Tags("인증 및 SSO (Authentication & SSO)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromQuery] string? returnUrl = null,
        [FromQuery] string? post_logout_redirect_uri = null,
        CancellationToken ct = default)
    {
        var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var userId = cookieResult.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // 이 사용자의 모든 활성 토큰(refresh_token 포함) 및 Authorization 정보 즉시 폐기(Bulk Revoke)
            await tokenManager.RevokeAsync(userId, null, null, null, ct);
            await authorizationManager.RevokeAsync(userId, null, null, null, ct);

            // 🌟 신규 분리 테이블 refreshtokens도 함께 일괄 폐기(IsRevoked = true)
            var activeRefreshTokens = await db.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync(ct);
            foreach (var rt in activeRefreshTokens)
            {
                rt.IsRevoked = true;
                rt.RevokedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            logger.LogInformation("전역 로그아웃 - 사용자(UserId: {UserId})의 모든 활성 토큰 및 refreshtokens DB 폐기(Revoke) 완료", userId);
        }

        // AuthServer SSO 세션 쿠키 명시적 파기
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var authCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };
        Response.Cookies.Delete("AuthServer_SSO_Cookie", authCookieOptions);
        Response.Cookies.Delete("AuthServer_SSO_Cookie", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        });
        Response.Cookies.Delete("AuthServer_SSO_Cookie");

        var targetRedirectUri = !string.IsNullOrWhiteSpace(post_logout_redirect_uri)
            ? post_logout_redirect_uri
            : !string.IsNullOrWhiteSpace(returnUrl)
                ? returnUrl
                : null;

        if (string.IsNullOrWhiteSpace(targetRedirectUri) && Request.HasFormContentType && Request.Form.ContainsKey("post_logout_redirect_uri"))
        {
            targetRedirectUri = Request.Form["post_logout_redirect_uri"].ToString();
        }

        // 브라우저 직접 요청이거나 redirect 주소가 명시된 경우 302 리다이렉트
        if (!string.IsNullOrWhiteSpace(targetRedirectUri) || (Request.Headers.Accept.ToString().Contains("text/html") && HttpMethods.IsGet(Request.Method)))
        {
            var finalRedirect = !string.IsNullOrWhiteSpace(targetRedirectUri) ? targetRedirectUri : "http://localhost:3000/";
            return Redirect(finalRedirect);
        }

        return Ok(new
        {
            success = true,
            message = "SSO 전역 세션이 성공적으로 종료되었으며 AuthServer_SSO_Cookie 및 발급된 모든 토큰이 폐기되었습니다.",
            redirectUrl = targetRedirectUri ?? "http://localhost:3000/"
        });
    }

    [HttpPost("authorization-code")]
    [Tags("OIDC 인가 및 토큰 관리 (OIDC Authorization & Token)")]
    [ProducesResponseType(typeof(GenerateAuthCodeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAuthorizationCode(
        [FromBody] GenerateAuthCodeRequestDto request,
        CancellationToken ct = default)
    {
        var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var userEmail = cookieResult.Principal?.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return Unauthorized(new ErrorResponseDto
            {
                Success = false,
                Message = "로그인 세션이 만료되었거나 인증되지 않았습니다. 로그인 후 다시 시도해 주세요."
            });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail, ct);
        if (user is null)
        {
            return Unauthorized(new ErrorResponseDto
            {
                Success = false,
                Message = "사용자 정보를 찾을 수 없습니다."
            });
        }

        // 302 리다이렉트 URL로부터 OIDC 파라미터 파싱
        string? clientId = null;
        string? redirectUri = null;
        string? codeChallenge = null;
        string? state = null;
        string? scope = null;

        var rawUrl = !string.IsNullOrWhiteSpace(request.AuthorizeUrl) ? request.AuthorizeUrl : request.RedirectUrl;
        if (!string.IsNullOrWhiteSpace(rawUrl))
        {
            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("client_id", out var qClientId)) clientId = qClientId.ToString();
                if (query.TryGetValue("redirect_uri", out var qRedirectUri)) redirectUri = qRedirectUri.ToString();
                if (query.TryGetValue("code_challenge", out var qChallenge)) codeChallenge = qChallenge.ToString();
                if (query.TryGetValue("state", out var qState)) state = qState.ToString();
                if (query.TryGetValue("scope", out var qScope)) scope = qScope.ToString();
            }
            else if (rawUrl.Contains('?'))
            {
                var queryString = rawUrl[(rawUrl.IndexOf('?') + 1)..];
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString);
                if (query.TryGetValue("client_id", out var qClientId)) clientId = qClientId.ToString();
                if (query.TryGetValue("redirect_uri", out var qRedirectUri)) redirectUri = qRedirectUri.ToString();
                if (query.TryGetValue("code_challenge", out var qChallenge)) codeChallenge = qChallenge.ToString();
                if (query.TryGetValue("state", out var qState)) state = qState.ToString();
                if (query.TryGetValue("scope", out var qScope)) scope = qScope.ToString();
            }
        }

        clientId = string.IsNullOrWhiteSpace(clientId) ? "company-homepage" : clientId;
        redirectUri = string.IsNullOrWhiteSpace(redirectUri) ? "https://localhost:7001/api/auth/oidc-callback" : redirectUri;
        codeChallenge = string.IsNullOrWhiteSpace(codeChallenge) ? "E9Melhoa2OwvFrGMTJguCH5rtx647BZKE66_L9l7E60" : codeChallenge;
        state = string.IsNullOrWhiteSpace(state) ? Guid.NewGuid().ToString("N") : state;
        scope = string.IsNullOrWhiteSpace(scope) ? "openid profile email roles offline_access" : scope;

        // 1. 32바이트 암호학적 보안 난수로 일회용 인가 코드(authorization_code) 생성
        var authCodeBytes = RandomNumberGenerator.GetBytes(32);
        var authorizationCode = Base64UrlTextEncoder.Encode(authCodeBytes);

        // 2. 인가 코드 SHA-256 해시 계산 (MariaDB에는 해시만 저장)
        var authorizationCodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationCode))).ToLowerInvariant();

        // 3. PKCE code_challenge SHA-256 해시 계산 (MariaDB에는 해시만 저장)
        var challengeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codeChallenge))).ToLowerInvariant();

        // 4. MariaDB AuthorizationCodes 테이블에 평문 없이 해시값만 저장
        var authRecord = new AuthorizationCode
        {
            AuthorizationCodeHash = authorizationCodeHash,
            CodeChallengeHash = challengeHash,
            ClientId = clientId,
            RedirectUri = redirectUri,
            UserId = user.Id.ToString(),
            UserEmail = user.Email,
            Scope = scope,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1),
            IsUsed = false
        };

        db.AuthorizationCodes.Add(authRecord);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("MariaDB에 인가 코드 및 PKCE 챌린지 해시 저장 완료 - AuthCodeHash: {CodeHash}, ChallengeHash: {ChallengeHash}, User: {User}",
            authorizationCodeHash, challengeHash, user.Email);

        // 5. 클라이언트로 전달할 리다이렉트 URL 구성
        var issuer = "https://localhost:7213/";
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(authorizationCode)}",
            $"authorization_code={Uri.EscapeDataString(authorizationCode)}",
            $"state={Uri.EscapeDataString(state)}",
            $"iss={Uri.EscapeDataString(issuer)}"
        };
        var separator = authRecord.RedirectUri.Contains('?') ? "&" : "?";
        var redirectUrl = $"{authRecord.RedirectUri}{separator}{string.Join("&", queryParams)}";

        return Ok(new GenerateAuthCodeResponseDto
        {
            Success = true,
            Message = "302 리다이렉트 URL로부터 파라미터가 자동 파싱되었으며, 인가 코드 및 PKCE 해시가 평문 없이 MariaDB에 안전하게 저장되었습니다.",
            AuthorizationCode = authorizationCode,
            State = state,
            Issuer = issuer,
            RedirectUrl = redirectUrl,
            DatabaseRecord = new AuthCodeDbRecordDto
            {
                Id = authRecord.Id,
                AuthorizationCodeHash = authorizationCodeHash,
                CodeChallengeHash = challengeHash,
                ClientId = authRecord.ClientId,
                UserEmail = authRecord.UserEmail,
                UserId = authRecord.UserId,
                CreatedAtUtc = authRecord.CreatedAtUtc,
                ExpiresAtUtc = authRecord.ExpiresAtUtc,
                LifetimeSeconds = 60
            },
            ClientParameters = new
            {
                authorization_code = authorizationCode,
                code = authorizationCode,
                state = state,
                iss = issuer,
                client_id = authRecord.ClientId,
                code_challenge = codeChallenge,
                code_challenge_method = "S256",
                redirect_uri = authRecord.RedirectUri,
                scope = authRecord.Scope
            }
        });
    }

    /// <summary>
    /// [DB 저장 내역 조회] MariaDB에 저장된 인가 코드 및 PKCE Code Challenge 해시 목록 조회
    /// </summary>
    /// <remarks>
    /// MariaDB `authorizationcodes` 테이블에 기록된 인가 코드 발급 및 해시 저장 내역을 실시간으로 확인합니다.
    /// </remarks>
    [HttpGet("authorization-codes")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(List<AuthorizationCode>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssuedAuthorizationCodes(CancellationToken ct)
    {
        var records = await db.AuthorizationCodes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        return Ok(records);
    }

    [HttpPost("validate-pkce")]
    [Tags("OIDC 인가 및 토큰 관리 (OIDC Authorization & Token)")]
    [ProducesResponseType(typeof(ValidatePkceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidatePkceExchange([FromBody] BackchannelTokenExchangeRequestDto request, CancellationToken ct)
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
        var record = await db.AuthorizationCodes
            .FirstOrDefaultAsync(x => x.AuthorizationCodeHash == authorizationCodeHash, ct);

        if (record is null)
        {
            logger.LogWarning("PKCE 검증 실패 - 존재하지 않는 인가 코드 해시: {AuthorizationCodeHash}", authorizationCodeHash);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "유효하지 않거나 존재하지 않는 인가 코드(authorization_code)입니다."
            });
        }

        if (record.IsUsed)
        {
            logger.LogWarning("PKCE 검증 실패 - 이미 사용된 인가 코드 (ID: {Id})", record.Id);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "이미 사용(교환) 완료된 인가 코드(authorization_code)입니다. 보안을 위해 재사용할 수 없습니다."
            });
        }

        if (record.ExpiresAtUtc < DateTime.UtcNow)
        {
            logger.LogWarning("PKCE 검증 실패 - 인가 코드 만료 (ID: {Id}, 만료일시: {Exp})", record.Id, record.ExpiresAtUtc);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "인가 코드(authorization_code)가 만료되었습니다. 다시 인증을 요청해 주세요."
            });
        }

        // PKCE S256 검증: SHA256(Base64Url(SHA256(code_verifier))) == record.CodeChallengeHash
        using var sha256 = SHA256.Create();
        var calculatedChallenge = Base64UrlEncoder.Encode(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.CodeVerifier)));
        var calculatedChallengeHash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(calculatedChallenge))).ToLowerInvariant();

        if (!string.Equals(calculatedChallengeHash, record.CodeChallengeHash, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("PKCE 검증 실패 - 계산된 Challenge 해시: {CalcHash}, 저장된 Challenge 해시: {StoredHash}", calculatedChallengeHash, record.CodeChallengeHash);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "PKCE 보안 검증 실패: 전달된 code_verifier의 S256 해시가 인가 시 제출된 code_challenge의 해시와 일치하지 않습니다."
            });
        }

        logger.LogInformation("서버 간 PKCE 및 인가 코드 유효성 사전 검증 성공 - User: {User}, CodeId: {CodeId}", record.UserEmail, record.Id);

        return Ok(new ValidatePkceResponseDto
        {
            Success = true,
            Message = "서버 간 PKCE 토큰 교환 사전 검증 성공: 인가 코드 유효성 및 PKCE S256 해시 일치가 모두 정상 확인되었습니다.",
            AuthorizationCodeValidation = new
            {
                authorization_code = authorizationCode,
                isValid = true,
                isUsed = record.IsUsed,
                isRedeemed = record.IsUsed,
                expiresAtUtc = record.ExpiresAtUtc,
                remainingSeconds = Math.Max(0, (int)(record.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds)
            },
            PkceValidation = new
            {
                codeVerifier = request.CodeVerifier,
                calculatedChallenge,
                calculatedChallengeHash,
                storedChallengeHash = record.CodeChallengeHash,
                matched = true
            },
            TargetUser = new
            {
                email = record.UserEmail,
                userId = record.UserId,
                subject = record.UserId,
                clientId = record.ClientId,
                scope = record.Scope
            }
        });
    }

    [HttpPost("token-exchange")]
    [Tags("OIDC 인가 및 토큰 관리 (OIDC Authorization & Token)")]
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
        var record = await db.AuthorizationCodes
            .FirstOrDefaultAsync(x => x.AuthorizationCodeHash == authorizationCodeHash, ct);

        if (record is null)
        {
            logger.LogWarning("토큰 교환 실패 - 존재하지 않는 인가 코드 해시: {AuthorizationCodeHash}", authorizationCodeHash);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "유효하지 않거나 존재하지 않는 인가 코드(authorization_code)입니다."
            });
        }

        if (record.IsUsed)
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

        // PKCE S256 검증: SHA256(Base64Url(SHA256(code_verifier))) == record.CodeChallengeHash
        using var sha256 = SHA256.Create();
        var calculatedChallenge = Base64UrlEncoder.Encode(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.CodeVerifier)));
        var calculatedChallengeHash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(calculatedChallenge))).ToLowerInvariant();

        if (!string.Equals(calculatedChallengeHash, record.CodeChallengeHash, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("PKCE 검증 실패 - 계산된 Challenge 해시: {CalcHash}, 저장된 Challenge 해시: {StoredHash}", calculatedChallengeHash, record.CodeChallengeHash);
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "PKCE 보안 검증 실패: 전달된 code_verifier의 S256 해시가 인가 시 제출된 code_challenge의 해시와 일치하지 않습니다."
            });
        }

        // 인가 코드 사용 완료 처리
        record.IsUsed = true;
        record.UsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // 사용자 조회
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == record.UserEmail, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == record.UserId, ct);

        var roleName = user?.Role.ToString() ?? "Admin";
        var displayName = user?.DisplayName ?? "테스트 사용자";
        var email = user?.Email ?? record.UserEmail;
        var sub = user?.Id.ToString() ?? record.UserId;

        // 1. JWT Access Token 생성 (ResourceServer와 호환)
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKeyForDevelopmentTesting1234567890!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessClaims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, sub),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, displayName),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, displayName),
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
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, displayName),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, displayName),
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

        // 3. Refresh Token 생성 및 refreshtokens 테이블에 해시 저장 (14일 수명)
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var refreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();

        db.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenHash = refreshTokenHash,
            UserId = sub,
            UserEmail = email,
            ClientId = record.ClientId,
            Scope = record.Scope,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            IsRevoked = false
        });
        await db.SaveChangesAsync(ct);

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
                userId = sub,
                email,
                name = displayName,
                displayName,
                userName = displayName,
                role = roleName
            },
            PkceValidation = new
            {
                codeVerifier = request.CodeVerifier,
                calculatedChallenge,
                calculatedChallengeHash,
                storedChallengeHash = record.CodeChallengeHash,
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

    [HttpPost("refresh-token")]
    [Tags("OIDC 인가 및 토큰 관리 (OIDC Authorization & Token)")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var refreshToken = !string.IsNullOrWhiteSpace(request.RefreshToken)
            ? request.RefreshToken
            : request.Token;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "리프레시 토큰(refresh_token)은 필수 값입니다."
            });
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
        var tokenRecord = await db.RefreshTokens.FirstOrDefaultAsync(r => r.RefreshTokenHash == tokenHash, ct);

        if (tokenRecord is null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "유효하지 않거나 존재하지 않는 리프레시 토큰입니다."
            });
        }

        if (tokenRecord.IsRevoked)
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "이미 폐기(로그아웃 또는 회전 교체)된 리프레시 토큰입니다. 보안을 위해 다시 로그인해 주세요."
            });
        }

        if (tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "리프레시 토큰이 만료되었습니다. 다시 로그인해 주세요."
            });
        }

        // 1. 신규 Refresh Token 발급 및 기존 토큰 폐기 (Token Rotation)
        var newRefreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var newRefreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newRefreshToken))).ToLowerInvariant();

        tokenRecord.IsRevoked = true;
        tokenRecord.RevokedAtUtc = DateTime.UtcNow;
        tokenRecord.ReplacedByTokenHash = newRefreshTokenHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenHash = newRefreshTokenHash,
            UserId = tokenRecord.UserId,
            UserEmail = tokenRecord.UserEmail,
            ClientId = tokenRecord.ClientId,
            Scope = tokenRecord.Scope,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            IsRevoked = false
        });
        await db.SaveChangesAsync(ct);

        // 2. 사용자 조회 및 신규 Access/ID Token 발급
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == tokenRecord.UserEmail, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == tokenRecord.UserId, ct);

        var roleName = user?.Role.ToString() ?? "Admin";
        var displayName = user?.DisplayName ?? "테스트 사용자";
        var email = user?.Email ?? tokenRecord.UserEmail;
        var sub = user?.Id.ToString() ?? tokenRecord.UserId;

        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKeyForDevelopmentTesting1234567890!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessClaims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, sub),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, displayName),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, roleName),
            new("role", roleName),
            new("scope", tokenRecord.Scope)
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

        var idClaims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, sub),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, displayName),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Name, displayName),
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

        logger.LogInformation("Refresh Token 회전(Rotation) 및 Access/ID Token 재발급 성공 - User: {User}, TokenId: {TokenId}", email, tokenRecord.Id);

        return Ok(new
        {
            success = true,
            message = "Refresh Token 회전(Rotation) 및 Access/ID Token 재발급이 성공적으로 완료되었습니다.",
            token_type = "Bearer",
            access_token = accessToken,
            id_token = idToken,
            refresh_token = newRefreshToken,
            expires_in = 900,
            scope = tokenRecord.Scope,
            user = new
            {
                sub,
                userId = sub,
                email,
                name = displayName,
                displayName,
                userName = displayName,
                role = roleName
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
    public string? DisplayName { get; set; }
    public string? UserName
    {
        get => DisplayName;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(DisplayName)) DisplayName = value; }
    }
    public string? Role { get; set; }
    public IEnumerable<object>? Claims { get; set; }
}

public class GenerateAuthCodeRequestDto
{
    /// <summary>
    /// Step 01(GET /api/auth/access-sso)에서 발급받은 302 리다이렉트 URL (Location 헤더 또는 authorizeUrl 주소 전체)
    /// (URL 내에 포함된 code_challenge, state, client_id, redirect_uri, scope 등이 자동 추출되므로 별도 정보 입력이 필요 없습니다)
    /// </summary>
    [Required]
    [JsonPropertyName("redirectUrl")]
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// (호환성 지원) authorizeUrl 키 이름 호환
    /// </summary>
    [JsonPropertyName("authorizeUrl")]
    public string? AuthorizeUrl
    {
        get => RedirectUrl;
        set { if (!string.IsNullOrWhiteSpace(value)) RedirectUrl = value; }
    }
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
    public string CodeChallengeHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Subject => UserId;
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

public class ValidatePkceResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? AuthorizationCodeValidation { get; set; }
    public object? PkceValidation { get; set; }
    public object? DatabaseIntegrity { get; set; }
    public object? TargetUser { get; set; }
}

public class RefreshTokenRequestDto
{
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
