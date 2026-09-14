namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu người dùng gửi lên khi đăng nhập.
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Email đăng nhập.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu người dùng nhập.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}