namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu trả về sau khi đăng nhập thành công.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// JWT token dùng để xác thực các request tiếp theo.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// ID người dùng.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Họ và tên người dùng.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Email người dùng.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò của người dùng.
    /// </summary>
    public string Role { get; set; } = string.Empty;
}