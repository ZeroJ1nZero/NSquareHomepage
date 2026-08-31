namespace Domain.Entities;

public class CompanyHistoryItem
{
    public string EventDate { get; }
    public string Date => EventDate;
    public string Content { get; }

    public CompanyHistoryItem(string eventDate, string content)
    {
        EventDate = eventDate;
        Content = content;
    }
}
