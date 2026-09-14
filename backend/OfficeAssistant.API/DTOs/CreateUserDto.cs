namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu nhận vào khi tạo người dùng mới.
/// </summary>
public class CreateUserDto
{
    /// <summary>
    /// Họ và tên người dùng.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Email đăng nhập của người dùng.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu ban đầu.
    /// Tạm thời nhận vào để kiểm tra API.
    /// Phần xác thực thực tế sẽ được xử lý ở bước JWT/Auth.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò người dùng.
    /// Mặc định là Staff.
    /// </summary>
    public string Role { get; set; } = "Staff";
}