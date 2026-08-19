using System.Text.Json.Serialization;

namespace ServiceServer.Api.DTOs;

public record CompanyHistoryDto(int Id, DateOnly Date, string Content, DateTime CreatedAt);
public record CreateCompanyHistoryDto(DateOnly Date, string Content);
public record UpdateCompanyHistoryDto(DateOnly Date, string Content);

public class HistoryItemDto
{
    private DateOnly? _data;

    [JsonPropertyName("data")]
    public DateOnly Data
    {
        get => _data ?? DateOnly.FromDateTime(DateTime.Today);
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
    public HistoryItemDto(DateOnly data, string content)
    {
        _data = data;
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
