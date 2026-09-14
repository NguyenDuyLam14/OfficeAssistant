namespace OfficeAssistant.API.Services;

/// <summary>
/// Chứa các cấu hình cần thiết để tạo và xác thực JWT.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Khóa bí mật dùng để ký JWT.
    /// Trong môi trường thực tế không nên đưa khóa này lên GitHub.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên hệ thống phát hành token.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Đối tượng nhận token.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Thời gian token có hiệu lực, tính bằng phút.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 120;
}