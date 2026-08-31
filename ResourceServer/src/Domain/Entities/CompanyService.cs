namespace Domain.Entities;

public class CompanyService
{
    public int Id { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public CompanyService() { }

    public CompanyService(string content)
    {
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string content)
    {
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }
}
