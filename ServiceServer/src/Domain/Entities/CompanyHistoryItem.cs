namespace Domain.Entities;

public class CompanyHistoryItem
{
    public string Date { get; }
    public string Content { get; }

    public CompanyHistoryItem(string date, string content)
    {
        Date = date;
        Content = content;
    }
}
