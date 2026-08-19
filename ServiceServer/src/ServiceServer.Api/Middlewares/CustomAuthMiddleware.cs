namespace ServiceServer.Api.Middlewares;

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
        if (context.Request.Cookies.TryGetValue(".NsqHomepage.ServiceSession", out var cookieValue))
        {
            _logger.LogDebug("ServiceSession Cookie 수신 확인");
        }

        await _next(context);
    }
}
