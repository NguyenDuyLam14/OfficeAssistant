namespace OfficeAssistant.API.Models;

/// <summary>
/// Đại diện cho người dùng trong hệ thống.
/// </summary>
public class User
{
    /// <summary>
    /// Khóa chính của người dùng.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Họ và tên người dùng.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ email của người dùng.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu đã được mã hóa.
    /// Không lưu mật khẩu dạng văn bản thuần túy.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò của người dùng.
    /// Ví dụ: Admin, Staff.
    /// </summary>
    public string Role { get; set; } = "Staff";

    /// <summary>
    /// Trạng thái hoạt động của tài khoản.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Thời điểm tạo tài khoản.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}