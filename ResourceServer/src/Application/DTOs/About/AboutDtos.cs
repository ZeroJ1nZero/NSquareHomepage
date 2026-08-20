namespace Application.DTOs.About;

public record AboutDto(string Content, DateTime UpdatedAt);
public record UpdateAboutDto(string Content);
