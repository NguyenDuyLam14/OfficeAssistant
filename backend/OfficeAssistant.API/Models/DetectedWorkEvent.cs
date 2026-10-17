
namespace OfficeAssistant.API.Models;

/// <summary>
/// Lưu lịch công việc được AI phát hiện từ email Gmail.
/// Dữ liệu mới phát hiện phải chờ người dùng xác nhận.
/// </summary>
public class DetectedWorkEvent
{
    public int DetectedWorkEventId { get; set; }

    /// <summary>
    /// Người dùng OfficeAssistant sở hữu lịch được phát hiện.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// ID email gốc do Gmail cung cấp.
    /// Dùng để truy vết và tránh xử lý trùng email.
    /// </summary>
    public string GmailMessageId { get; set; } = string.Empty;

    /// <summary>
    /// Loại công việc: Meeting, Deadline, Appointment...
    /// </summary>
    public string EventType { get; set; } = "Other";

    /// <summary>
    /// Tiêu đề lịch công việc.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung mô tả hoặc thông tin liên quan.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Thời gian bắt đầu theo UTC, nếu xác định được.
    /// </summary>
    public DateTime? StartAtUtc { get; set; }

    /// <summary>
    /// Thời hạn hoàn thành theo UTC, nếu xác định được.
    /// </summary>
    public DateTime? DueAtUtc { get; set; }

    /// <summary>
    /// Múi giờ dùng khi diễn giải thời gian trong email.
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// Trạng thái:
    /// PendingReview, Confirmed, Dismissed hoặc Completed.
    /// </summary>
    public string Status { get; set; } = "PendingReview";

    /// <summary>
    /// Thời điểm phát hiện.
    /// </summary>
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm người dùng xác nhận, nếu có.
    /// </summary>
    public DateTime? ConfirmedAtUtc { get; set; }

    /// <summary>
    /// Quan hệ với tài khoản OfficeAssistant.
    /// </summary>
    public User User { get; set; } = null!;
}
