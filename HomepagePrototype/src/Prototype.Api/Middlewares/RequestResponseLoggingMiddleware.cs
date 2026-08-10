using System.Diagnostics;

namespace Prototype.Api.Middlewares;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        _logger.LogInformation("[HTTP REQUEST] {Method} {Path}", request.Method, request.Path);

        await _next(context);

        stopwatch.Stop();
        var statusCode = context.Response.StatusCode;

        _logger.LogInformation("[HTTP RESPONSE] {Method} {Path} => Status {StatusCode} ({ElapsedMilliseconds}ms)",
            request.Method, request.Path, statusCode, stopwatch.ElapsedMilliseconds);
    }
}
