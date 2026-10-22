
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Quản lý thông báo của người dùng đang đăng nhập.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy UserId từ JWT đã xác thực.
    /// Không nhận UserId từ frontend.
    /// </summary>
    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out userId) && userId > 0;
    }

    /// <summary>
    /// GET /api/Notifications
    /// Lấy danh sách thông báo mới nhất của người dùng hiện tại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        // Giới hạn tham số phân trang để tránh truy vấn quá nhiều dữ liệu.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.AppNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var notifications = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.AppNotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.AppNotificationId,
                title = x.Title,
                message = x.Message,
                type = x.NotificationType,
                detectedWorkEventId = x.DetectedWorkEventId,
                isRead = x.IsRead,
                createdAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            unreadCount = await query.CountAsync(
                x => !x.IsRead, cancellationToken),
            items = notifications
        });
    }

    /// <summary>
    /// GET /api/Notifications/unread-count
    /// Đếm số thông báo chưa đọc.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        var count = await _context.AppNotifications
            .CountAsync(
                x => x.UserId == userId && !x.IsRead,
                cancellationToken);

        return Ok(new
        {
            unreadCount = count
        });
    }

    /// <summary>
    /// PATCH /api/Notifications/{id}/read
    /// Đánh dấu một thông báo là đã đọc.
    /// </summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        // Điều kiện UserId bảo đảm người dùng không sửa thông báo
        // thuộc tài khoản khác.
        var notification = await _context.AppNotifications
            .FirstOrDefaultAsync(
                x => x.AppNotificationId == id
                     && x.UserId == userId,
                cancellationToken);

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thông báo."
            });
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            message = "Đã đánh dấu thông báo là đã đọc.",
            id = notification.AppNotificationId,
            isRead = notification.IsRead
        });
    }

    /// <summary>
    /// PATCH /api/Notifications/read-all
    /// Đánh dấu tất cả thông báo của người dùng là đã đọc.
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        var unreadNotifications = await _context.AppNotifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        if (unreadNotifications.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            message = "Đã đánh dấu tất cả thông báo là đã đọc.",
            updatedCount = unreadNotifications.Count
        });
    }
}
