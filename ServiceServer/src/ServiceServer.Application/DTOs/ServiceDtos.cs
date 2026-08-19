namespace ServiceServer.Application.DTOs;

public record ServiceDto(string Service, DateTime UpdatedAt);
public record UpdateServiceDto(string Service);
