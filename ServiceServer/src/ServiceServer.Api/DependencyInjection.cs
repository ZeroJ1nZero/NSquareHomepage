using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using ServiceServer.Api.Services;

namespace ServiceServer.Api;

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
                ### 🏢 엔스퀘어 사내 통합 홈페이지 SSO 2-Track 파이프라인 (sso_pipeline_specification.md 준수)

                ---
                #### 🔄 SSO 로그인 시퀀스 파이프라인 (Step 1 ~ Step 12)
                ```
                [Step 1] 클라이언트 -> 보호된 기능 접근 요청 (또는 GET /api/auth/login)
                [Step 2] 서비스서버 -> PKCE 원본키(verifier), 해시키(challenge), CSRF 검증키(state) 생성 & 세션 보관 후 302 리다이렉트
                [Step 3] 클라이언트 -> 인증서버(:7213) 접근 (challenge + state 전달)
                [Step 4~7] 인증서버 -> 아이디/비번(test@company.local / Test1234!) 검증 및 SSO 쿠키 발급
                [Step 8] 인증서버 -> 1회용 인가 코드(code) 발급 및 서비스서버로 리다이렉트
                [Step 9] 클라이언트 -> 서비스서버(/signin-oidc)로 인가 코드(code) + state 제출
                [Step 10] 서비스서버 -> state 대조(CSRF 방어) 후 세션의 verifier와 함께 인증서버로 백채널 토큰 교환 요청
                [Step 11] 인증서버 -> PKCE 대조 검증 후 Access Token(JWT) + Refresh Token 발급 (백채널)
                [Step 12] 서비스서버 -> 토큰을 세션에 은폐 보관하고 브라우저에 .NsqHomepage.ServiceSession 세션 쿠키 발급
                ```

                ---
                #### 🛣️ 2-Track 리소스 처리 파이프라인
                - **[트랙 A: 공개 조회]**: `GET /api/Home/*` ➔ 로그인 세션 검사 없이 ResourceServer(:7002) 대행 호출
                - **[트랙 B: 관리자 CUD]**: `PUT /api/Home/*` ➔ 세션 쿠키 및 Role == Admin 검증 후 Access Token을 Bearer 헤더로 첨부하여 ResourceServer(:7002) 대행 호출 (미인증 시 Step 1~2 302 리다이렉트)
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

        // 2. OIDC State & PKCE 서비스 등록
        services.AddScoped<IOidcStateService, OidcStateService>();

        // 3. IdP 통신용 HttpClient 등록 (HTTPS 개발 인증서 허용)
        services.AddHttpClient("IdpClient")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        // 4. ResourceServer API 대행 호출용 Typed HttpClient 등록
        var resourceServerBaseUrl = configuration["ResourceServer:BaseUrl"] ?? "https://localhost:7002";
        services.AddHttpClient<IResourceApiClient, ResourceApiClient>(client =>
        {
            client.BaseAddress = new Uri(resourceServerBaseUrl.TrimEnd('/') + "/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // 5. BFF 서비스 세션 쿠키 인증 등록 (sso_pipeline_specification.md 규격)
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
