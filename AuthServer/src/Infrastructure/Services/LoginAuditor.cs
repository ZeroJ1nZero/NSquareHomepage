using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Services;

public class LoginAuditor(AppDbContext db) : ILoginAuditor
{
    public async Task RecordAsync(string loginId, string ipAddress, bool succeeded, CancellationToken ct = default)
    {
        db.LoginLogs.Add(new LoginLog
        {
            LoginId = loginId,
            IpAddress = ipAddress,
            Succeeded = succeeded,
            AttemptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
