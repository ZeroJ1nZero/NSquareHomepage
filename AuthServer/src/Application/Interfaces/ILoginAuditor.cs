namespace Application.Interfaces;

public interface ILoginAuditor
{
    Task RecordAsync(string userName, string ipAddress, bool succeeded, CancellationToken ct = default);
}
