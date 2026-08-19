namespace ServiceServer.Application.DTOs;

public record AboutDto(string Introduction, DateTime UpdatedAt);
public record UpdateAboutDto(string Introduction);
