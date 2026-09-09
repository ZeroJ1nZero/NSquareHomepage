using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using Application.UseCases;
using Domain.Entities;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
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
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestHeadersTotalSize = 65536; // 64KB
    serverOptions.Limits.MaxRequestBufferSize = 1048576;     // 1MB
    if (signingCert != null)
    {
        serverOptions.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = signingCert;
        });
    }
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RegisterUserUseCase>();

// ── OpenIddict 서버 (OIDC 엔드포인트) ────────────────────────
builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo");

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, Scopes.Roles);

        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange() // PKCE 강제
               .AllowRefreshTokenFlow()
               .AllowPasswordFlow();

        // 인가 코드(1분), 엑세스 토큰(15분), 리프레시 토큰(14일)의 수명을 설정합니다.
        options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1));
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        options.DisableAccessTokenEncryption();
        options.DisableTokenStorage();

        // 운영용 인증서가 제공되면 이를 사용하고, 없으면 개발용 인증서로 폴백합니다.
        if (encryptionCert != null)
            options.AddEncryptionCertificate(encryptionCert);
        else
            options.AddDevelopmentEncryptionCertificate();
        if (signingCert != null)
            options.AddSigningCertificate(signingCert);
        else
            options.AddDevelopmentSigningCertificate();
        options.AddEventHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>(builder =>
            builder.UseScopedHandler<SaveAuthorizationCodeHandler>());

        options.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(builder =>
            builder.UseScopedHandler<SaveRefreshTokenHandler>());

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
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SaveAuthorizationCodeHandler>();
builder.Services.AddScoped<SaveRefreshTokenHandler>();

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
        options.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
        options.ConfigObject.AdditionalItems["operationsSorter"] = "alpha";
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
    var result = await useCase.ExecuteAsync(req.Email, req.DisplayName, req.Password, req.Role, ct);
    return result.Succeeded
        ? Results.Ok(new Web.Controllers.UserResponseDto
        {
            Success = true,
            Message = "사용자가 성공적으로 등록되었습니다.",
            UserId = result.UserId,
            Email = req.Email.Trim(),
            DisplayName = req.DisplayName.Trim(),
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

public class SaveAuthorizationCodeHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public SaveAuthorizationCodeHandler(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context)
    {
        var code = context.Response?.Code;
        if (!string.IsNullOrEmpty(code))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
            var challenge = context.Request?.CodeChallenge ?? string.Empty;
            var challengeHash = !string.IsNullOrEmpty(challenge)
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(challenge))).ToLowerInvariant()
                : string.Empty;

            var userEmail = context.Request?.Username ?? string.Empty;
            var subject = string.Empty;
            if (httpContext?.User.Identity?.IsAuthenticated == true)
            {
                subject = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                userEmail = httpContext.User.FindFirstValue(ClaimTypes.Email) 
                         ?? httpContext.User.FindFirstValue("email") 
                         ?? userEmail;
            }

            if (string.IsNullOrWhiteSpace(userEmail) && long.TryParse(subject, out var uid))
            {
                var u = await _db.Users.FindAsync([uid], httpContext?.RequestAborted ?? default);
                if (u != null)
                {
                    userEmail = u.Email;
                }
            }

            var scopesList = context.Request?.GetScopes();
            var scopes = scopesList.HasValue ? string.Join(" ", scopesList.Value) : string.Empty;

            _db.AuthorizationCodes.Add(new AuthorizationCode
            {
                AuthorizationCodeHash = codeHash,
                CodeChallengeHash = challengeHash,
                ClientId = context.Request?.ClientId ?? "company-homepage",
                RedirectUri = context.Request?.RedirectUri ?? string.Empty,
                UserId = subject,
                UserEmail = userEmail,
                Scope = scopes,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1),
                IsUsed = false
            });
            await _db.SaveChangesAsync(httpContext?.RequestAborted ?? default);
        }
    }
}

public class SaveRefreshTokenHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyTokenResponseContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public SaveRefreshTokenHandler(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        var refreshToken = context.Response?.RefreshToken;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
            var subject = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var email = httpContext?.User.FindFirstValue(ClaimTypes.Email) 
                     ?? httpContext?.User.FindFirstValue("email") 
                     ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) && long.TryParse(subject, out var uid))
            {
                var u = await _db.Users.FindAsync([uid], httpContext?.RequestAborted ?? default);
                if (u != null)
                {
                    email = u.Email;
                }
            }

            var scopesList = context.Request?.GetScopes();
            var scopes = scopesList.HasValue ? string.Join(" ", scopesList.Value) : string.Empty;

            _db.RefreshTokens.Add(new RefreshToken
            {
                RefreshTokenHash = tokenHash,
                UserId = subject,
                UserEmail = email,
                ClientId = context.Request?.ClientId ?? "company-homepage",
                Scope = scopes,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
                IsRevoked = false
            });
            await _db.SaveChangesAsync(httpContext?.RequestAborted ?? default);
        }
    }
}


