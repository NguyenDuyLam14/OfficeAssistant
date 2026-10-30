namespace OfficeAssistant.API.Models;

/// <summary>
/// Bản tóm tắt email do AI tạo, lưu lại để không gọi AI lặp lại
/// (tiết kiệm quota Gemini).
/// </summary>
public class EmailSummaryCache
{
    public int EmailSummaryCacheId { get; set; }
    public int UserId { get; set; }
    public string GmailMessageId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chiến dịch gửi email hàng loạt của một người dùng.
/// Status: Draft, Queued, Running, Completed, Cancelled, Failed.
/// </summary>
public class EmailCampaign
{
    public int EmailCampaignId { get; set; }
    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    /// <summary>Nội dung thư, có thể chứa biến dạng {{Ten}}.</summary>
    public string BodyTemplate { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    /// <summary>Ghi chú trạng thái (đang chờ hạn mức, lỗi kết nối...).</summary>
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public List<EmailCampaignRecipient> Recipients { get; set; } = new();
}

/// <summary>
/// Một người nhận trong chiến dịch.
/// Status: Pending, Sent, Failed.
/// </summary>
public class EmailCampaignRecipient
{
    public int EmailCampaignRecipientId { get; set; }
    public int EmailCampaignId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }

    /// <summary>JSON {"cột": "giá trị"} dùng để điền biến trong mẫu thư.</summary>
    public string? DataJson { get; set; }

    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public EmailCampaign Campaign { get; set; } = null!;
}
