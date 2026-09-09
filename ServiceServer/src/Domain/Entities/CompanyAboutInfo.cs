namespace Domain.Entities;

public class CompanyAboutInfo
{
    public string Content { get; }
    public DateTime UpdatedAt { get; }

    public CompanyAboutInfo(string content, DateTime updatedAt)
    {
        Content = content;
        UpdatedAt = updatedAt;
    }
}
