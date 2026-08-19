using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ResourceServer.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpClient();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ResourceServer API (Data Resource Server)",
                Version = "v1",
                Description = "엔스퀘어 사내 홈페이지 데이터 리소스 API 서버 (CRUD & Zero-Trust JWT 인가)"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "발급받은 JWT Bearer 토큰 값만 입력하세요. ('Bearer ' 접두사는 자동으로 추가됩니다)"
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

            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (System.IO.File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
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
                    ValidateIssuer = false, // 개발 편의 및 다중 호스트(7213/5123) 허용
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    RoleClaimType = "role",
                    NameClaimType = "name"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
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
                        }
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
