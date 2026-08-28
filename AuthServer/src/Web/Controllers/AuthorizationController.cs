using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Web.Controllers;

/// <summary>
/// OIDC 표준 엔드포인트. 프로토콜 처리(파라미터 검증, 코드/토큰 발급, PKCE)는
/// 전부 OpenIddict가 수행하고, 여기서는 "누구를 로그인시킬지"만 결정한다.
/// </summary>
public class AuthorizationController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    ILoginAuditor auditor) : Controller
{
    [HttpGet("~/connect/authorize"), HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect 요청을 읽을 수 없습니다.");

        // 인증서버 세션(쿠키) 확인 — 있으면 로그인 화면 없이 통과 (SSO의 핵심)
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Domain.Entities.User? user = null;
        if (long.TryParse(result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            user = await db.Users.FindAsync(userId);
        if (user is null) // SSO 쿠키(AuthServer_SSO_Cookie)가 없는 경우에만 로그인 페이지로 Challenge 이동
        {
            return Challenge(new AuthenticationProperties
            {
                // 로그인 성공 후 이 authorize 요청 전체로 복귀
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                    Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList()),
            }, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
                .SetClaim(Claims.Email, user.Email)
                .SetClaim(Claims.Name, user.UserName)
                .SetClaim(Claims.Role, user.Role.ToString()); // 클라이언트가 역할별 화면 분기에 사용

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        // ponytail: 사내 신뢰 클라이언트 전제 → 동의(consent) 화면 없이 자동 동의.
        // 외부 클라이언트를 받게 되면 동의 화면 추가.
        foreach (var claim in principal.Claims)
            claim.SetDestinations(GetDestinations(claim));

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect 요청을 읽을 수 없습니다.");

        if (request.IsPasswordGrantType())
        {
            var email = request.Username;
            var password = request.Password;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, HttpContext.RequestAborted);

            var succeeded = user is not null &&
                !string.IsNullOrEmpty(password) &&
                hasher.VerifyHashedPassword(user, user.PasswordHash, password) is not PasswordVerificationResult.Failed;

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await auditor.RecordAsync(email ?? "unknown", ip, succeeded);

            if (!succeeded || user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "이메일 또는 비밀번호가 일치하지 않습니다.",
                    }));
            }

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, user.Id.ToString())
                    .SetClaim(Claims.Email, user.Email)
                    .SetClaim(Claims.Name, user.UserName)
                    .SetClaim(Claims.Role, user.Role.ToString());

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            foreach (var claim in principal.Claims)
                claim.SetDestinations(GetDestinations(claim));

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            throw new InvalidOperationException("지원하지 않는 grant type입니다.");

        // 인가 코드/리프레시 토큰에 담긴 principal 복원
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // 토큰 발급 시점에 사용자가 여전히 존재하는지 재확인
        if (!long.TryParse(result.Principal?.GetClaim(Claims.Subject), out var subjectId) ||
            !await db.Users.AnyAsync(u => u.Id == subjectId))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "토큰이 더 이상 유효하지 않습니다.",
                }));
        }

        if (request.IsAuthorizationCodeGrantType() && !string.IsNullOrEmpty(request.Code))
        {
            var codeHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Code))).ToLowerInvariant();
            var issuedCode = await db.AuthorizationCodes.FirstOrDefaultAsync(c => c.AuthorizationCodeHash == codeHash, HttpContext.RequestAborted);
            if (issuedCode != null)
            {
                issuedCode.IsRedeemed = true;
                issuedCode.RedeemedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(HttpContext.RequestAborted);
            }
        }

        foreach (var claim in result.Principal!.Claims)
            claim.SetDestinations(GetDestinations(claim));

        return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    public async Task<IActionResult> UserInfo()
    {
        Domain.Entities.User? user = null;
        if (long.TryParse(User.GetClaim(Claims.Subject), out var userId))
            user = await db.Users.FindAsync(userId);
        if (user is null)
            return Challenge(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        return Ok(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString(),
            [Claims.Email] = user.Email,
            [Claims.Name] = user.UserName,
            [Claims.Role] = user.Role.ToString(),
        });
    }



    private static IEnumerable<string> GetDestinations(Claim claim) => claim.Type switch
    {
        // name/email/role은 id_token에도 포함 (클라이언트가 사용자 표시·화면 분기에 사용)
        Claims.Name or Claims.Email or Claims.Role or ClaimTypes.Name or ClaimTypes.Email or ClaimTypes.Role => [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };
}
