using System.Security.Claims;
using System.Threading.RateLimiting;
using Application.UseCases;
using Domain.Entities;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Web;
using Web.Middleware;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

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

        // (options.AddSigningCertificate / AddEncryptionCertificate)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough();
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
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

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
internal record ChangeRoleRequest(UserRole Role);
