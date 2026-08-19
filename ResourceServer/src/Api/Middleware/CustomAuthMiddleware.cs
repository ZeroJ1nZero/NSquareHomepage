namespace Api.Middleware;

public class CustomAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomAuthMiddleware> _logger;

    public CustomAuthMiddleware(RequestDelegate next, ILogger<CustomAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            _logger.LogDebug("Authorization Header 검증: {Header}", authHeader.ToString());
        }

        await _next(context);
    }
}
