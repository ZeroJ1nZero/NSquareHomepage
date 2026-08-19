using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;

namespace Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ServiceServer (BFF 세션 관리자 & 게이트웨이)",
                Version = "v1",
                Description = """
                ### 🏢 엔스퀘어 사내 통합 홈페이지 API 명세서 (Clean Architecture 4계층 적용)

                ---
                #### 📌 Swagger UI 그룹 분류 기준
                1. **`1. [파이프라인 1] SSO 로그인 & 토큰 발급 (Step 1 ~ Step 12)`**: OIDC Authorization Code Flow + PKCE 핵심 파이프라인
                2. **`2. [파이프라인 2-A] 공개 데이터 조회 (트랙 A: 로그인 불필요)`**: 일반 방문자용 공개 GET 조회 엔드포인트
                3. **`3. [파이프라인 2-B] 관리자 데이터 처리 (트랙 B: Role == Admin)`**: Role == Admin 및 Bearer 토큰 기반 CUD 엔드포인트
                4. **`4. [기타 / 보조 기능] 세션 관리 및 개발/테스트 도구 (Pipeline 외)`**: 수동 토큰 교환 등 개발/테스트 도구
                """
            });

            options.AddSecurityDefinition("CookieAuth", new OpenApiSecurityScheme
            {
                Name = "Cookie",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Description = "BFF 서비스 세션 쿠키 (.NsqHomepage.ServiceSession)"
            });

            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (System.IO.File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        // 1. 세션 상태 관리 (PKCE verifier, CSRF state 보관용)
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = ".NsqHomepage.SessionData";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.IdleTimeout = TimeSpan.FromMinutes(15);
        });

        // 2. BFF 서비스 세션 쿠키 인증 등록 (sso_pipeline_specification.md 규격)
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = ".NsqHomepage.ServiceSession";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        return services;
    }
}
