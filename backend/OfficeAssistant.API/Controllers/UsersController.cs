
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// API quản lý người dùng.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    // Dùng PasswordHasher để băm mật khẩu trước khi lưu vào cơ sở dữ liệu.
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách người dùng.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Tạo người dùng mới.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        // Kiểm tra họ tên.
        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest(new
            {
                message = "Họ và tên không được để trống."
            });
        }

        // Chuẩn hóa dữ liệu đầu vào.
        var fullName = dto.FullName.Trim();
        var email = dto.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "Email không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new
            {
                message = "Mật khẩu không được để trống."
            });
        }

        // Kiểm tra email sau khi loại bỏ khoảng trắng thừa.
        var emailExists = await _context.Users
            .AnyAsync(user => user.Email == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email này đã tồn tại."
            });
        }

        // Tạo User trước để dùng đối tượng này khi băm mật khẩu.
        var user = new User
        {
            FullName = fullName,
            Email = email,
            // Không lấy vai trò từ dữ liệu gửi lên để tránh tự cấp quyền Admin.
            Role = "Staff",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Băm mật khẩu; tuyệt đối không lưu mật khẩu nguyên văn.
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            dto.Password
        );

        // Lưu người dùng vào SQL Server.
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Không trả về PasswordHash trong phản hồi API.
        return CreatedAtAction(
            nameof(GetUsers),
            new { id = user.UserId },
            new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            }
        );
    }
}
