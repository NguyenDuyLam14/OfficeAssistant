using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// API xử lý đăng nhập và xác thực người dùng.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthController(
        ApplicationDbContext context,
        JwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
        _passwordHasher = new PasswordHasher<User>();
    }

    /// <summary>
    /// Đăng nhập bằng email và mật khẩu.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // Kiểm tra email.
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new
            {
                message = "Email không được để trống."
            });
        }

        // Kiểm tra mật khẩu.
        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new
            {
                message = "Mật khẩu không được để trống."
            });
        }

        // Tìm người dùng theo email.
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == dto.Email.Trim()
            );

        // Không tiết lộ tài khoản có tồn tại hay không.
        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không chính xác."
            });
        }

        // Kiểm tra tài khoản có đang hoạt động hay không.
        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Tài khoản đã bị vô hiệu hóa."
            });
        }

        // Kiểm tra mật khẩu đã hash.
        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password
        );

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không chính xác."
            });
        }

        // Tạo JWT sau khi xác thực thành công.
        var token = _jwtService.GenerateToken(user);

        // Trả thông tin đăng nhập cho frontend.
        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        });
    }

    /// <summary>
    /// Người dùng đang đăng nhập tự đổi mật khẩu.
    /// POST api/auth/change-password
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập mật khẩu hiện tại."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword) ||
            dto.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới phải có ít nhất 8 ký tự."
            });
        }

        if (dto.NewPassword == dto.CurrentPassword)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới phải khác mật khẩu hiện tại."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);

        if (user == null)
        {
            return Unauthorized();
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, dto.CurrentPassword);

        // Dùng 400 thay vì 401: axios interceptor ở frontend sẽ tự
        // đăng xuất khi gặp 401, không phù hợp với lỗi nhập sai mật khẩu.
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Mật khẩu hiện tại không đúng."
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(
            user, dto.NewPassword);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }
}