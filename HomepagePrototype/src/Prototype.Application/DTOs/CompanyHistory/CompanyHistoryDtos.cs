using System.Text.Json.Serialization;

namespace Prototype.Application.DTOs.CompanyHistory;

public record CompanyHistoryDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt
);

public record CreateCompanyHistoryDto(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("content")] string Content
);

public record UpdateCompanyHistoryDto(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("content")] string Content
);
