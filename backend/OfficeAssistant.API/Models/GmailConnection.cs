
namespace OfficeAssistant.API.Models;

/// <summary>
/// Lưu thông tin kết nối Gmail của một người dùng OfficeAssistant.
/// Không lưu Client Secret của ứng dụng Google trong bảng này.
/// </summary>
public class GmailConnection
{
    /// <summary>
    /// Khóa chính của kết nối Gmail.
    /// </summary>
    public int GmailConnectionId { get; set; }

    /// <summary>
    /// Người dùng OfficeAssistant sở hữu kết nối này.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Địa chỉ Gmail đã kết nối.
    /// </summary>
    public string GmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// Access token dùng để gọi Gmail API.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token dùng để xin access token mới khi cần.
    /// Có thể rỗng nếu Google không trả refresh token.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Thời điểm access token hết hạn theo UTC.
    /// </summary>
    public DateTime? TokenExpiresAtUtc { get; set; }

    /// <summary>
    /// Thời điểm kết nối được tạo.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm kết nối được cập nhật gần nhất.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quan hệ với tài khoản OfficeAssistant.
    /// </summary>
    public User User { get; set; } = null!;
}
