namespace Prototype.Application.DTOs.About;

public record AboutDto(string Introduction, DateTime UpdatedAt);
public record UpdateAboutDto(string Introduction);
