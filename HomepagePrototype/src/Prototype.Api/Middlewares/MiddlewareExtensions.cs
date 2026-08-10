namespace Prototype.Api.Middlewares;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCustomExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestResponseLoggingMiddleware>();
    }

    public static IApplicationBuilder UseCustomAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CustomAuthMiddleware>();
    }

    // 개방-폐쇄 원칙(OCP): 미들웨어 파이프라인 확장을 유연하게 관리할 수 있는 일괄 등록 확장 메서드
    public static IApplicationBuilder UseCustomApiMiddlewares(this IApplicationBuilder app)
    {
        app.UseCustomExceptionHandling();
        app.UseRequestResponseLogging();
        app.UseCustomAuthentication();

        return app;
    }
}
