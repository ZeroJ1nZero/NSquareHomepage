namespace Domain.Entities;

public class CompanyHistory
{
    public int Id { get; private set; }
    public DateOnly EventDate { get; private set; }

    /// <summary>
    /// (하위 호환용 Date 별칭)
    /// </summary>
    public DateOnly Date => EventDate;

    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private CompanyHistory() { }

    public CompanyHistory(DateOnly eventDate, string content)
    {
        EventDate = eventDate;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(DateOnly eventDate, string content)
    {
        EventDate = eventDate;
        Content = content;
    }
}
