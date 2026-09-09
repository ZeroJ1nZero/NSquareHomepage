namespace Domain.Entities;

public class CompanyAbout
{
    public int Id { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public CompanyAbout() { }

    public CompanyAbout(string content)
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
