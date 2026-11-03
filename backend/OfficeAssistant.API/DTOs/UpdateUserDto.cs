namespace OfficeAssistant.API.DTOs;

/// <summary>
/// Dữ liệu Admin gửi lên khi cập nhật người dùng.
/// </summary>
public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;

    // Chỉ chấp nhận "Admin" hoặc "Staff".
    public string Role { get; set; } = "Staff";

    public bool IsActive { get; set; } = true;
}