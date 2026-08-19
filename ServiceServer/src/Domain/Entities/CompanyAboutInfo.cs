namespace Domain.Entities;

public class CompanyAboutInfo
{
    public string Introduction { get; }
    public DateTime UpdatedAt { get; }

    public CompanyAboutInfo(string introduction, DateTime updatedAt)
    {
        Introduction = introduction;
        UpdatedAt = updatedAt;
    }
}
