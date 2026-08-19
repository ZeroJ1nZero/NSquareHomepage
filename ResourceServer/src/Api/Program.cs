using Microsoft.EntityFrameworkCore;
using Api;
using Api.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. DI 서비스 등록 (Clean Architecture 4계층)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// DB 마이그레이션/테이블 자동 생성
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

// 2. HTTP 요청 파이프라인 & 미들웨어 설정
app.UseCustomApiMiddlewares();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ResourceServer API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthentication(); // JWT Bearer 토큰 검증
app.UseAuthorization();

app.MapControllers();

app.Run();
