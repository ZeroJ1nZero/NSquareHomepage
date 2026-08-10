namespace Prototype.Domain.Entities;

public class CompanyInfo
{
    public int Id { get; private set; } = 1;
    public string Introduction { get; private set; } = string.Empty;
    public string Service { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public CompanyInfo() { }

    public void UpdateIntroduction(string introduction)
    {
        Introduction = introduction;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateService(string service)
    {
        Service = service;
        UpdatedAt = DateTime.UtcNow;
    }
}
