
using System.Text.Json.Serialization;

namespace OfficeAssistant.API.DTOs.Email;

/// <summary>
/// Kết quả nhận diện sự kiện công việc từ nội dung email bằng AI.
/// Đây là dữ liệu đề xuất, chưa phải sự kiện đã được người dùng xác nhận.
/// </summary>
public sealed class WorkEventExtractionResult
{
    [JsonPropertyName("hasEvent")]
    public bool HasEvent { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = "Other";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm bắt đầu theo ISO 8601; chuỗi rỗng nếu chưa xác định.
    /// </summary>
    [JsonPropertyName("startAt")]
    public string StartAt { get; set; } = string.Empty;

    /// <summary>
    /// Hạn hoàn thành theo ISO 8601; chuỗi rỗng nếu chưa xác định.
    /// </summary>
    [JsonPropertyName("dueAt")]
    public string DueAt { get; set; } = string.Empty;
}
