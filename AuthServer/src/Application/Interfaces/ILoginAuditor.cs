namespace Application.Interfaces;

public interface ILoginAuditor
{
    Task RecordAsync(string loginId, string ipAddress, bool succeeded, CancellationToken ct = default);
}
