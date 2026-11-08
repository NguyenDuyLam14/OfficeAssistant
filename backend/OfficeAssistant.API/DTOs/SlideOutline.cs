using System.Text.Json.Serialization;

namespace OfficeAssistant.API.DTOs;

public sealed class SlideOutline
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("subtitle")] public string Subtitle { get; set; } = string.Empty;
    [JsonPropertyName("slides")] public List<SlideItem> Slides { get; set; } = new();
}

public sealed class SlideItem
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("bullets")] public List<string> Bullets { get; set; } = new();
    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
}