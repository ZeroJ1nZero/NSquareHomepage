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

// DB 마이그레이션/테이블 자동 생성
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
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
