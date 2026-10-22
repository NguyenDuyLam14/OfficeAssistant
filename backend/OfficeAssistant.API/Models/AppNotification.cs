namespace OfficeAssistant.API.Models;

/// <summary>
/// Thông báo hiển thị bên trong ứng dụng OfficeAssistant.
/// </summary>
public class AppNotification
{
    public int AppNotificationId { get; set; }

    /// <summary>
    /// Người dùng nhận thông báo.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Tiêu đề thông báo.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung thông báo.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Phân loại, ví dụ: DetectedWorkEvent, Reminder.
    /// </summary>
    public string NotificationType { get; set; } = "General";

    /// <summary>
    /// Liên kết đến lịch công việc được phát hiện, nếu có.
    /// </summary>
    public int? DetectedWorkEventId { get; set; }

    /// <summary>
    /// Đã đọc hay chưa.
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Thời điểm tạo thông báo.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public DetectedWorkEvent? DetectedWorkEvent { get; set; }
}
