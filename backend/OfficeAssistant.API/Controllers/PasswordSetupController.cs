using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// API tạm thời dùng để thiết lập mật khẩu đã được hash
/// cho tài khoản kiểm thử.
/// 
/// Sau khi hoàn thành Authentication có thể xóa Controller này.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PasswordSetupController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public PasswordSetupController(ApplicationDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    /// <summary>
    /// Hash mật khẩu cho một tài khoản theo UserId.
    /// </summary>
    [HttpPost("{userId:int}")]
    public async Task<IActionResult> SetupPassword(
        int userId,
        [FromBody] string password)
    {
        // Kiểm tra mật khẩu có được gửi lên hay không.
        if (string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new
            {
                message = "Mật khẩu không được để trống."
            });
        }

        // Tìm người dùng trong database.
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy người dùng."
            });
        }

        // Hash mật khẩu bằng ASP.NET Core PasswordHasher.
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            password
        );

        // Lưu password hash vào SQL Server.
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã thiết lập mật khẩu thành công."
        });
    }
}