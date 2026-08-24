using Web;
using Web.Middleware;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. DI 서비스 등록 (Clean Architecture 4계층 구조)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebServices(builder.Configuration);

var app = builder.Build();

// 2. HTTP 요청 파이프라인 & 미들웨어 설정
app.UseCustomWebMiddlewares();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServiceServer API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseSession();        // 세션 미들웨어 (PKCE verifier, CSRF state 보관)
app.UseAuthentication(); // 서비스 세션 쿠키 인증
app.UseAuthorization();

app.MapControllers();

app.Run();
