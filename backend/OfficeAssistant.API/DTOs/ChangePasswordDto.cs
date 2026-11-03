namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu người dùng gửi lên khi tự đổi mật khẩu.
/// </summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}