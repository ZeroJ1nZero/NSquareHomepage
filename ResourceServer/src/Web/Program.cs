using Microsoft.EntityFrameworkCore;
using Web;
using Web.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. DI 서비스 등록 (Clean Architecture 4계층)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebServices(builder.Configuration);

var app = builder.Build();

// DB 마이그레이션/테이블 자동 생성 및 초기 데이터 시딩
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();

    if (!dbContext.CompanyAbouts.Any())
    {
        dbContext.CompanyAbouts.Add(new Domain.Entities.CompanyAbout("NSquare는 혁신적인 엔터프라이즈 보안 및 클라우드 솔루션을 제공하는 글로벌 IT 기업입니다."));
    }

    if (!dbContext.CompanyServices.Any())
    {
        dbContext.CompanyServices.Add(new Domain.Entities.CompanyService("클라우드 네이티브 아키텍처 컨설팅, OIDC/OAuth2 엔터프라이즈 보안 플랫폼, 고성능 분산 웹 서비스 개발"));
    }

    if (!dbContext.CompanyHistories.Any())
    {
        dbContext.CompanyHistories.AddRange(
            new Domain.Entities.CompanyHistory(new DateOnly(2024, 1, 1), "엔스퀘어(NSquare) 법인 설립"),
            new Domain.Entities.CompanyHistory(new DateOnly(2025, 6, 1), "엔터프라이즈 OIDC SSO 인증 솔루션 런칭"),
            new Domain.Entities.CompanyHistory(new DateOnly(2026, 1, 1), "글로벌 B2B 클라우드 플랫폼 서비스 확장")
        );
    }

    dbContext.SaveChanges();
}

// 2. HTTP 요청 파이프라인 & 미들웨어 설정
app.UseCustomWebMiddlewares();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ResourceServer API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthentication();        // 1단계: JWT Bearer Access Token 서명 및 수명 검증
app.UseCustomAuthentication();  // 2단계: Zero-Trust 관리자 권한(Role == Admin) 이중 검증 미들웨어
app.UseAuthorization();         // ASP.NET Core 역할 기반 인가 [Authorize(Roles = "Admin")]

app.MapControllers();

app.Run();
