namespace OfficeAssistant.API.Models;

/// <summary>
/// Ghi nhớ email đã được AI phân tích để không phân tích lại.
/// </summary>
public class ProcessedGmailMessage
{
    public int ProcessedGmailMessageId { get; set; }
    public int UserId { get; set; }
    public string GmailMessageId { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}