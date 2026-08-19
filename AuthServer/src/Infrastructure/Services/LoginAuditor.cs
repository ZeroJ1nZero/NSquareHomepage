using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Services;

public class LoginAuditor(AppDbContext db) : ILoginAuditor
{
    public async Task RecordAsync(string userName, string ipAddress, bool succeeded, CancellationToken ct = default)
    {
        db.LoginAudits.Add(new LoginAudit
        {
            UserName = userName,
            IpAddress = ipAddress,
            Succeeded = succeeded,
            AttemptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
