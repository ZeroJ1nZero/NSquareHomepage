using Web;
using Web.Middleware;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestHeadersTotalSize = 65536; // 64KB
    serverOptions.Limits.MaxRequestBufferSize = 1048576;     // 1MB
});

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
    c.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
    c.ConfigObject.AdditionalItems["operationsSorter"] = "alpha";
});

app.UseCors("AllowAll");
app.UseSession();        // 세션 미들웨어 (PKCE verifier, CSRF state 보관)
app.UseAuthentication(); // 서비스 세션 쿠키 인증
app.UseAuthorization();

app.MapControllers();

app.Run();
