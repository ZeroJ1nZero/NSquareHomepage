using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Application.UseCases;
using Domain.Entities;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Web;
using Web.Middleware;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// 운영용 인증서(PFX)를 구성에서 읽어들여 OpenIddict 서명/암호화 및 Kestrel HTTPS에 사용합니다.
X509Certificate2? signingCert = null;
X509Certificate2? encryptionCert = null;
var signingPath = builder.Configuration["Certificates:Signing:PfxPath"];
var signingPassword = builder.Configuration["Certificates:Signing:Password"];
if (!string.IsNullOrEmpty(signingPath) && File.Exists(signingPath))
{
    signingCert = new X509Certificate2(signingPath, signingPassword, X509KeyStorageFlags.DefaultKeySet);
}
var encryptionPath = builder.Configuration["Certificates:Encryption:PfxPath"];
var encryptionPassword = builder.Configuration["Certificates:Encryption:Password"];
if (!string.IsNullOrEmpty(encryptionPath) && File.Exists(encryptionPath))
{
    encryptionCert = new X509Certificate2(encryptionPath, encryptionPassword, X509KeyStorageFlags.DefaultKeySet);
}

// Kestrel HTTPS 기본 옵션에 발견된 인증서를 할당합니다 (있을 경우). 운영 환경에서 HTTPS를 강제할 때 사용.
if (signingCert != null)
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = signingCert;
        });
    });
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RegisterUserUseCase>();

// ── OpenIddict 서버 (OIDC 엔드포인트) ────────────────────────
builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo")
               .SetEndSessionEndpointUris("connect/logout");

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess);

        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange() // PKCE 강제
               .AllowRefreshTokenFlow();

        //엑세스 토큰과 리프레시 토큰의 수명을 설정합니다.
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        options.DisableAccessTokenEncryption();

        // 운영용 인증서가 제공되면 이를 사용하고, 없으면 개발용 인증서로 폴백합니다.
        if (encryptionCert != null)
            options.AddEncryptionCertificate(encryptionCert);
        else
            options.AddDevelopmentEncryptionCertificate();
        if (signingCert != null)
            options.AddSigningCertificate(signingCert);
        else
            options.AddDevelopmentSigningCertificate();
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough();

        if (builder.Environment.IsDevelopment())
        {
            options.UseAspNetCore().DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer(); // userinfo 등에서 자체 발급 토큰 검증
        options.UseAspNetCore();
    });

// ASP.NET Identity 대신 순수 쿠키 인증 — 로그인 검증은 Login 페이지가 직접 수행
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");

// ── 기본 보안: IP당 분당 60회 요청 제한 ──────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
            }));
});

builder.Services.AddMemoryCache();

// 별도 프로젝트로 도는 클라이언트(홈페이지)의 브라우저 요청 허용
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<SeedData>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<IpBlockMiddleware>();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.MapPost("/api/register", async (RegisterRequest req, RegisterUserUseCase useCase, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(req.Email, req.UserName, req.Password, ct);
    return result.Succeeded ? Results.Ok() : Results.BadRequest(new { result.Errors });
});

// 아이디/비밀번호 검증 전용 API 엔드포인트
app.MapPost("/api/auth/validate", async (
    ValidateCredentialsRequest req,
    AppDbContext db,
    Microsoft.AspNetCore.Identity.IPasswordHasher<User> hasher,
    Application.Interfaces.ILoginAuditor auditor,
    HttpContext context,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { success = false, message = "이메일과 비밀번호를 입력해주세요." });
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
    var succeeded = user is not null &&
        hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) is not Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;

    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    await auditor.RecordAsync(req.Email, ip, succeeded);

    if (!succeeded || user is null)
    {
        return Results.Json(new { success = false, message = "아이디 또는 비밀번호가 올바르지 않습니다." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new
    {
        success = true,
        message = "인증 성공 (아이디 및 비밀번호 일치)",
        user = new
        {
            id = user.Id,
            email = user.Email,
            userName = user.UserName,
            role = user.Role.ToString()
        }
    });
});

// 역할 변경은 관리자만. 쿠키 클레임이 아닌 DB의 현재 역할로 판정 — 강등이 즉시 반영된다.
// ponytail: cross-site 요청은 쿠키 기본 SameSite=Lax가 차단. 외부 도메인 관리 UI가 생기면 antiforgery 추가.
app.MapPut("/api/users/{id:long}/role", async (long id, ChangeRoleRequest req, ClaimsPrincipal caller, AppDbContext db, CancellationToken ct) =>
{
    if (!Enum.IsDefined(req.Role))
        return Results.BadRequest(new { Errors = new[] { "올바르지 않은 역할입니다." } });

    if (!long.TryParse(caller.FindFirstValue(ClaimTypes.NameIdentifier), out var callerId) ||
        (await db.Users.FindAsync([callerId], ct))?.Role != UserRole.Admin)
        return Results.Forbid();

    var user = await db.Users.FindAsync([id], ct);
    if (user is null)
        return Results.NotFound();

    user.Role = req.Role;
    await db.SaveChangesAsync(ct);
    return Results.Ok();
}).RequireAuthorization();

app.Run();

internal record RegisterRequest(string Email, string UserName, string Password);
internal record ValidateCredentialsRequest(string Email, string Password);
internal record ChangeRoleRequest(UserRole Role);

