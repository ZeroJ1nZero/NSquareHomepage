namespace Prototype.Api.Middlewares;

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
            // 토큰 파싱 및 Authentication/Authorization 컨텍스트 구성 가능
        }

        await _next(context);
    }
}
