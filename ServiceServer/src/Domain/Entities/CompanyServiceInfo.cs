namespace Domain.Entities;

public class CompanyServiceInfo
{
    public string Service { get; }
    public DateTime UpdatedAt { get; }

    public CompanyServiceInfo(string service, DateTime updatedAt)
    {
        Service = service;
        UpdatedAt = updatedAt;
    }
}
