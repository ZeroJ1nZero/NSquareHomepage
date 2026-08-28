namespace Web.Middleware;

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
        if (context.Request.Cookies.ContainsKey(".Nsq.About.Session") ||
            context.Request.Cookies.ContainsKey(".Nsq.Service.Session") ||
            context.Request.Cookies.ContainsKey(".Nsq.History.Session"))
        {
            _logger.LogDebug("위치별 서비스 세션 쿠키 수신 확인");
        }

        await _next(context);
    }
}
