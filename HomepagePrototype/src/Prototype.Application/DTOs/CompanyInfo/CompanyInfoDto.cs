using System.Text.Json.Serialization;

namespace Prototype.Application.DTOs.CompanyInfo;

public record CompanyInfoDto(
    [property: JsonPropertyName("introduction")] string Introduction,
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt
);

public record UpdateCompanyInfoDto(
    [property: JsonPropertyName("introduction")] string? Introduction,
    [property: JsonPropertyName("service")] string? Service
);
