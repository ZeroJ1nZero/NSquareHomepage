using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpClient();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ResourceServer API",
                Version = "v1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var authority = configuration["Authentication:Authority"] ?? "https://localhost:7213";
        var audience = configuration["Authentication:Audience"] ?? "company-homepage";
        var secretKey = "SuperSecretKeyForDevelopmentTesting1234567890!";
        var fallbackKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        // sso_pipeline_specification.md 규격: Zero-Trust 독립 JWT Bearer 토큰 서명 및 만료 검증
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = configuration.GetValue<bool>("Authentication:RequireHttpsMetadata", false);
                options.Audience = audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = fallbackKey,
                    ValidateIssuer = false, // 다중 호스트(7213/5123) 허용
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role",
                    NameClaimType = "name"
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearerAuth");
                        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                        {
                            var headerStr = authHeader.ToString();
                            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogInformation("📥 [1단계 토큰 수신] Authorization Bearer 토큰 수신 완료 (길이: {Length})", headerStr.Length);
                            }
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearerAuth");
                        if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                        {
                            var roleClaim = identity.FindFirst("role") ?? identity.FindFirst(System.Security.Claims.ClaimTypes.Role);
                            if (roleClaim != null)
                            {
                                if (!identity.HasClaim(System.Security.Claims.ClaimTypes.Role, roleClaim.Value))
                                {
                                    identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, roleClaim.Value));
                                }
                                if (!identity.HasClaim("role", roleClaim.Value))
                                {
                                    identity.AddClaim(new System.Security.Claims.Claim("role", roleClaim.Value));
                                }
                            }

                            var sub = identity.FindFirst("sub")?.Value ?? identity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                            var role = roleClaim?.Value ?? "User";
                            var exp = identity.FindFirst("exp")?.Value;
                            logger.LogInformation("✅ [Zero-Trust 1단계 검증 완료] Access Token 서명 및 유효성 확인 성공 - Sub: {Sub}, Role: {Role}, Exp: {Exp}", sub, role, exp);
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearerAuth");
                        logger.LogWarning("❌ [토큰 검증 실패] 사유: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearerAuth");
                        if (!string.IsNullOrWhiteSpace(context.Error))
                        {
                            logger.LogWarning("⚠️ [토큰 챌린지] 401 Unauthorized - 에러: {Error}, 설명: {Desc}", context.Error, context.ErrorDescription);
                        }
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearerAuth");
                        logger.LogWarning("🚫 [인가 차단] 관리자 권한(Role == Admin) 부족으로 접근 차단 (403 Forbidden)");
                        return Task.CompletedTask;
                    }
                };

                // 로컬 개발 환경의 자체 서명 인증서 허용
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
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
