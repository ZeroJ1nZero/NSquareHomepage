using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Web.Middleware;

/// <summary>BlockedIps 테이블에 등록된 IP를 차단한다.</summary>
public class IpBlockMiddleware(RequestDelegate next, IMemoryCache cache)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (ip is not null)
        {
            // ponytail: 1분 캐시로 매 요청 DB 조회 방지. 차단 반영이 최대 1분 늦는 것 허용.
            var blocked = await cache.GetOrCreateAsync("blocked-ip-set", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                var ips = await db.BlockedIps.Select(b => b.IpAddress).ToListAsync();
                return ips.ToHashSet();
            });

            if (blocked!.Contains(ip))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }
}
