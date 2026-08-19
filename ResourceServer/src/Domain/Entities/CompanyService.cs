namespace Domain.Entities;

public class CompanyService
{
    public int Id { get; private set; }
    public string Service { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public CompanyService() { }

    public CompanyService(string service)
    {
        Service = service;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string service)
    {
        Service = service;
        UpdatedAt = DateTime.UtcNow;
    }
}
