namespace Application.DTOs;

public record HistoryDto(int? Id, string Data, string Content);
public record HistoryItemDto(int? Id, string Date, string Content);
public record HistoryContainerDto(List<HistoryDto> History);
public record SaveHistoryRequestDto(List<HistoryDto> History);
