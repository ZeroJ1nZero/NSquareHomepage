namespace Prototype.Domain.Entities;

public class CompanyHistory
{
    public int Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private CompanyHistory() { }

    public CompanyHistory(DateOnly date, string content)
    {
        Date = date;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(DateOnly date, string content)
    {
        Date = date;
        Content = content;
    }
}
