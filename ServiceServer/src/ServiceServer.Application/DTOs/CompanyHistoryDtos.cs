namespace ServiceServer.Application.DTOs;

public record HistoryDto(string Data, string Content);
public record HistoryItemDto(string Date, string Content);
public record HistoryContainerDto(List<HistoryDto> History);
public record SaveHistoryRequestDto(List<HistoryDto> History);
