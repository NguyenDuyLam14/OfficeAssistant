using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services;

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
}