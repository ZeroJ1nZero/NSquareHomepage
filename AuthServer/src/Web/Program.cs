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

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, Scopes.Roles);

        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange() // PKCE 강제
               .AllowRefreshTokenFlow()
               .AllowPasswordFlow();

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

// ASP.NET Identity 대신 순수 쿠키 인증 — sso_pipeline_specification.md 규격: AuthServer_SSO_Cookie (14일 수명, SlidingExpiration)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthServer_SSO_Cookie";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

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

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AuthServer API",
        Version = "v1",
        Description = "N-SQUARE 통합 인증 및 사용자 관리 API (OIDC SSO & User Management)"
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});
builder.Services.AddHostedService<SeedData>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthServer API v1");
        options.RoutePrefix = "swagger";
    });
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

// 하위 호환성을 위한 /api/register 매핑 (Swagger UI에는 /api/users/register로 통일 노출)
app.MapPost("/api/register", async (Web.Controllers.RegisterUserRequest req, RegisterUserUseCase useCase, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(req.Email, req.UserName, req.Password, req.Role, ct);
    return result.Succeeded
        ? Results.Ok(new Web.Controllers.UserResponseDto
        {
            Success = true,
            Message = "사용자가 성공적으로 등록되었습니다.",
            UserId = result.UserId,
            Email = req.Email.Trim(),
            UserName = req.UserName.Trim(),
            Role = req.Role,
            RoleName = req.Role.ToString()
        })
        : Results.BadRequest(new Web.Controllers.ErrorResponseDto
        {
            Success = false,
            Message = "사용자 등록에 실패했습니다.",
            Errors = result.Errors
        });
}).ExcludeFromDescription();

app.Run();


