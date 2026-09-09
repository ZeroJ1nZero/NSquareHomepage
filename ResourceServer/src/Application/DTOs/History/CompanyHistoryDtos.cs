using System.Text.Json.Serialization;

namespace Application.DTOs.History;

public record CompanyHistoryDto(int Id, DateOnly EventDate, string Content, DateTime CreatedAt)
{
    [JsonPropertyName("date")]
    public DateOnly Date => EventDate;
}

public record CreateCompanyHistoryDto(DateOnly EventDate, string Content);
public record UpdateCompanyHistoryDto(DateOnly EventDate, string Content);

public class HistoryItemDto
{
    private DateOnly? _data;

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("eventDate")]
    public DateOnly EventDate
    {
        get => _data ?? DateOnly.FromDateTime(DateTime.Today);
        set => _data = value;
    }

    [JsonPropertyName("data")]
    public DateOnly Data
    {
        get => EventDate;
        set => _data = value;
    }

    [JsonPropertyName("date")]
    [JsonIgnore]
    public DateOnly? Date
    {
        get => null;
        set { if (value.HasValue) _data = value; }
    }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    public HistoryItemDto() { }
    public HistoryItemDto(DateOnly eventDate, string content)
    {
        _data = eventDate;
        Content = content;
    }
    public HistoryItemDto(int id, DateOnly eventDate, string content)
    {
        Id = id;
        _data = eventDate;
        Content = content;
    }
}

public class HistoryContainerDto
{
    [JsonPropertyName("history")]
    public List<HistoryItemDto> History { get; set; } = new();

    public HistoryContainerDto() { }
    public HistoryContainerDto(List<HistoryItemDto> history)
    {
        History = history;
    }
}

public class SaveHistoryRequestDto
{
    [JsonPropertyName("history")]
    public List<HistoryItemDto> History { get; set; } = new();
}
