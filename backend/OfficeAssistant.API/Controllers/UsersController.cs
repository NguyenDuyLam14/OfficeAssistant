
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;

using System.Security.Claims;

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
            // Endpoint chỉ dành cho Admin nên được phép chọn vai trò.
            Role = dto.Role == "Admin" ? "Admin" : "Staff",
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

    private int? GetCurrentUserId()
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id) ? id : null;
    }

    /// <summary>
    /// Cập nhật họ tên, vai trò, trạng thái của người dùng.
    /// PUT api/users/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest(new
            {
                message = "Họ và tên không được để trống."
            });
        }

        if (dto.Role != "Admin" && dto.Role != "Staff")
        {
            return BadRequest(new
            {
                message = "Vai trò không hợp lệ."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy người dùng."
            });
        }

        // Không cho Admin tự khóa hoặc tự hạ quyền chính mình,
        // tránh tình huống hệ thống không còn Admin nào.
        if (GetCurrentUserId() == id &&
            (dto.Role != "Admin" || !dto.IsActive))
        {
            return BadRequest(new
            {
                message = "Bạn không thể tự khóa hoặc tự hạ quyền tài khoản của mình."
            });
        }

        user.FullName = dto.FullName.Trim();
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            user.UserId,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt
        });
    }

    /// <summary>
    /// Đặt lại mật khẩu cho người dùng.
    /// POST api/users/{id}/reset-password
    /// </summary>
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id, ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) ||
            dto.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới phải có ít nhất 8 ký tự."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy người dùng."
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(
            user, dto.NewPassword);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã đặt lại mật khẩu." });
    }
}
