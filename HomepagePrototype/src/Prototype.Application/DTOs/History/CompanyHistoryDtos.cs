namespace Prototype.Application.DTOs.History;

public record CompanyHistoryDto(int Id, DateOnly Date, string Content, DateTime CreatedAt);
public record CreateCompanyHistoryDto(DateOnly Date, string Content);
public record UpdateCompanyHistoryDto(DateOnly Date, string Content);
