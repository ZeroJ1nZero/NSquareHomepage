using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Web.Pages;

[AutoValidateAntiforgeryToken]
public class LoginModel(AppDbContext db, IPasswordHasher<User> hasher, ILoginAuditor auditor) : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        // ponytail: 계정 잠금은 초기 버전에서 제외 — brute force는 IP 레이트리밋(분당 60회) + 감사 로그로 방어.
        // 필요해지면 LoginAudit 실패 횟수 기반 잠금 추가.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == Email);
        var succeeded = user is not null &&
            hasher.VerifyHashedPassword(user, user.PasswordHash, Password) is not PasswordVerificationResult.Failed;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await auditor.RecordAsync(Email, ip, succeeded);

        if (!succeeded)
        {
            Error = "아이디 또는 비밀번호가 올바르지 않습니다.";
            return Page();
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user!.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
        // 발급 시점 역할 고정 — [Authorize(Roles = ...)]용. 역할 변경은 재로그인 후 반영.
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect(returnUrl ?? "/");
    }
}
