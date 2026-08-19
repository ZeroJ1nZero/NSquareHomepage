using ServiceServer.Api;
using ServiceServer.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// 1. DI 서비스 등록 (BFF 세션 관리, OIDC PKCE 서비스 및 ResourceServer 게이트웨이 클라이언트)
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// 2. HTTP 요청 파이프라인 & 미들웨어 설정
app.UseCustomApiMiddlewares();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServiceServer API v1");
    c.RoutePrefix = "swagger";
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseSession();        // 세션 미들웨어 (PKCE verifier, CSRF state 보관)
app.UseAuthentication(); // 서비스 세션 쿠키 인증
app.UseAuthorization();

app.MapControllers();

app.Run();
