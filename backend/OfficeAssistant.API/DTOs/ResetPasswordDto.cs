namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu Admin gửi lên khi đặt lại mật khẩu cho người dùng.
/// </summary>
public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}