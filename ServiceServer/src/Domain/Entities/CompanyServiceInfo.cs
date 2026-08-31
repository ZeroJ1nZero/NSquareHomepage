namespace Domain.Entities;

public class CompanyServiceInfo
{
    public string Content { get; }
    public string Service => Content;
    public DateTime UpdatedAt { get; }

    public CompanyServiceInfo(string content, DateTime updatedAt)
    {
        Content = content;
        UpdatedAt = updatedAt;
    }
}
