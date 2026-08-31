using System.Text.Json.Serialization;

namespace Application.DTOs;

public class HistoryDto
{
    private string? _data;

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("eventDate")]
    public string EventDate
    {
        get => _data ?? string.Empty;
        set => _data = value;
    }

    [JsonPropertyName("data")]
    public string Data
    {
        get => EventDate;
        set => _data = value;
    }

    [JsonPropertyName("date")]
    [JsonIgnore]
    public string? Date
    {
        get => null;
        set { if (!string.IsNullOrWhiteSpace(value)) _data = value; }
    }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    public HistoryDto() { }
    public HistoryDto(int? id, string data, string content)
    {
        Id = id;
        _data = data;
        Content = content;
    }
}

public record HistoryItemDto(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("eventDate")] string EventDate,
    [property: JsonPropertyName("content")] string Content)
{
    [JsonPropertyName("date")]
    public string Date => EventDate;
}

public record HistoryContainerDto(
    [property: JsonPropertyName("history")] List<HistoryDto> History);

public record SaveHistoryRequestDto(
    [property: JsonPropertyName("history")] List<HistoryDto> History);
