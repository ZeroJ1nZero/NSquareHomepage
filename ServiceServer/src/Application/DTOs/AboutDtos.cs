namespace Application.DTOs;

public record AboutDto(string Content, DateTime UpdatedAt);
public record UpdateAboutDto(string Content);
