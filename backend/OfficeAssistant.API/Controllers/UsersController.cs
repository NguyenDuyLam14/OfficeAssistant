using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// API quản lý người dùng.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

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
        // Kiểm tra dữ liệu bắt buộc.
        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest(new
            {
                message = "Họ và tên không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
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

        // Kiểm tra email đã tồn tại chưa.
        var emailExists = await _context.Users
            .AnyAsync(user => user.Email == dto.Email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email này đã tồn tại."
            });
        }

        // Tạo đối tượng User.
        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim(),
            
            // Tạm thời lưu trực tiếp để kiểm tra luồng API.
            // Sẽ thay bằng password hashing ở bước xây dựng Authentication.
            PasswordHash = dto.Password,

            Role = string.IsNullOrWhiteSpace(dto.Role)
                ? "Staff"
                : dto.Role.Trim(),

            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Thêm User vào DbContext.
        _context.Users.Add(user);

        // Ghi dữ liệu xuống SQL Server.
        await _context.SaveChangesAsync();

        // Trả về thông tin User vừa tạo.
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