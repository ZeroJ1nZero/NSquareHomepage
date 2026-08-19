namespace Domain.Entities;

public class CompanyAbout
{
    public int Id { get; private set; }
    public string Introduction { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public CompanyAbout() { }

    public CompanyAbout(string introduction)
    {
        Introduction = introduction;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string introduction)
    {
        Introduction = introduction;
        UpdatedAt = DateTime.UtcNow;
    }
}
