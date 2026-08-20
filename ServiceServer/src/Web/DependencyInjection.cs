using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;

namespace Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ServiceServer API",
                Version = "v1"
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
