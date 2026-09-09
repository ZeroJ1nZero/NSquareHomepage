using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Web.Middleware;

/// <summary>
/// sso_pipeline_specification.md 규격에 맞춘 Zero-Trust 2차 이중 권한 인가 검증 미들웨어
/// </summary>
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
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // CUD(PUT, POST, DELETE) 관리자 엔드포인트에 대한 이중 보안 인가 검증
        if (HttpMethods.IsPut(method) || HttpMethods.IsPost(method) || HttpMethods.IsDelete(method))
        {
            var user = context.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
                var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("🔐 [Zero-Trust 2단계 이중 확인] 관리자(Role == Admin) 접근 권한 2차 확인 완료 - 요청자 ID: {Sub}, Method: {Method}, Path: {Path}", sub, method, path);
                }
                else
                {
                    _logger.LogWarning("🚫 [Zero-Trust 2단계 이중 확인 실패] 관리자 권한(Role == Admin)이 없는 사용자 접근 차단 - Sub: {Sub}, Role: {Role}, Path: {Path}", sub, role, path);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"message\":\"접근 권한이 없습니다. 관리자(Role == Admin)만 수정/저장할 수 있습니다.\"}");
                    return;
                }
            }
        }

        await _next(context);
    }
}
