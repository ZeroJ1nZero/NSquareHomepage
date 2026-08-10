using Microsoft.EntityFrameworkCore;
using Prototype.Api;
using Prototype.Api.Middlewares;
using Prototype.Application;
using Prototype.Infrastructure;
using Prototype.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. DI 서비스 등록
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddApiServices();

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Homepage Prototype API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
