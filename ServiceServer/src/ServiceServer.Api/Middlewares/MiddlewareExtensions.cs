namespace ServiceServer.Api.Middlewares;

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

    public static IApplicationBuilder UseCustomApiMiddlewares(this IApplicationBuilder app)
    {
        app.UseCustomExceptionHandling();
        app.UseRequestResponseLogging();
        app.UseCustomAuthentication();

        return app;
    }
}
