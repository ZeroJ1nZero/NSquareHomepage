using System.Text.Json.Serialization;

namespace Application.DTOs;

public record ServiceDto(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt)
{
    [JsonPropertyName("service")]
    public string Service => Content;
}

public class UpdateServiceDto
{
    private string _content = string.Empty;

    [JsonPropertyName("content")]
    public string Content
    {
        get => _content;
        set => _content = value;
    }

    [JsonPropertyName("service")]
    public string? Service
    {
        get => _content;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_content)) _content = value; }
    }

    public UpdateServiceDto() { }
    public UpdateServiceDto(string content)
    {
        _content = content;
    }
}
