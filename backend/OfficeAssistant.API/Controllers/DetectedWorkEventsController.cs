
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Quản lý các đề xuất công việc được phát hiện từ email.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DetectedWorkEventsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DetectedWorkEventsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy UserId từ JWT của người dùng hiện tại.
    /// </summary>
    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out userId) && userId > 0;
    }

    /// <summary>
    /// GET /api/DetectedWorkEvents
    /// Lấy danh sách đề xuất công việc, có thể lọc theo trạng thái.
    /// Ví dụ: ?status=PendingReview
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? status = null,
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.DetectedWorkEvents
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var allowedStatuses = new[]
            {
                "PendingReview",
                "Confirmed",
                "Dismissed",
                "Completed"
            };

            var matchedStatus = allowedStatuses.FirstOrDefault(
                x => string.Equals(
                    x, status, StringComparison.OrdinalIgnoreCase));

            if (matchedStatus == null)
            {
                return BadRequest(new
                {
                    message = "Trạng thái không hợp lệ."
                });
            }

            query = query.Where(x => x.Status == matchedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.DetectedAtUtc)
            .ThenByDescending(x => x.DetectedWorkEventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.DetectedWorkEventId,
                title = x.Title,
                description = x.Description,
                eventType = x.EventType,
                startAtUtc = x.StartAtUtc,
                dueAtUtc = x.DueAtUtc,
                timeZoneId = x.TimeZoneId,
                status = x.Status,
                detectedAtUtc = x.DetectedAtUtc,
                confirmedAtUtc = x.ConfirmedAtUtc,
                gmailMessageId = x.GmailMessageId
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            items
        });
    }

    /// <summary>
    /// GET /api/DetectedWorkEvents/{id}
    /// Xem chi tiết một đề xuất thuộc người dùng hiện tại.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEvent(
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

        var item = await _context.DetectedWorkEvents
            .AsNoTracking()
            .Where(x => x.DetectedWorkEventId == id && x.UserId == userId)
            .Select(x => new
            {
                id = x.DetectedWorkEventId,
                title = x.Title,
                description = x.Description,
                eventType = x.EventType,
                startAtUtc = x.StartAtUtc,
                dueAtUtc = x.DueAtUtc,
                timeZoneId = x.TimeZoneId,
                status = x.Status,
                detectedAtUtc = x.DetectedAtUtc,
                confirmedAtUtc = x.ConfirmedAtUtc,
                gmailMessageId = x.GmailMessageId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đề xuất công việc."
            });
        }

        return Ok(item);
    }

    /// <summary>
    /// PATCH /api/DetectedWorkEvents/{id}/confirm
    /// Người dùng xác nhận đề xuất.
    /// </summary>
    [HttpPatch("{id:int}/confirm")]
    public async Task<IActionResult> ConfirmEvent(
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

        var item = await _context.DetectedWorkEvents
            .FirstOrDefaultAsync(
                x => x.DetectedWorkEventId == id && x.UserId == userId,
                cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đề xuất công việc."
            });
        }

        if (item.Status != "PendingReview")
        {
            return Conflict(new
            {
                message = "Chỉ có thể xác nhận đề xuất đang chờ xem xét.",
                currentStatus = item.Status
            });
        }

        item.Status = "Confirmed";
        item.ConfirmedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã xác nhận đề xuất công việc.",
            id = item.DetectedWorkEventId,
            status = item.Status,
            confirmedAtUtc = item.ConfirmedAtUtc
        });
    }

    /// <summary>
    /// PATCH /api/DetectedWorkEvents/{id}/dismiss
    /// Người dùng từ chối đề xuất.
    /// </summary>
    [HttpPatch("{id:int}/dismiss")]
    public async Task<IActionResult> DismissEvent(
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

        var item = await _context.DetectedWorkEvents
            .FirstOrDefaultAsync(
                x => x.DetectedWorkEventId == id && x.UserId == userId,
                cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đề xuất công việc."
            });
        }

        if (item.Status != "PendingReview")
        {
            return Conflict(new
            {
                message = "Chỉ có thể từ chối đề xuất đang chờ xem xét.",
                currentStatus = item.Status
            });
        }

        item.Status = "Dismissed";

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã từ chối đề xuất công việc.",
            id = item.DetectedWorkEventId,
            status = item.Status
        });
    }

    /// <summary>
    /// PATCH /api/DetectedWorkEvents/{id}/complete
    /// Đánh dấu công việc đã xác nhận là hoàn thành.
    /// </summary>
    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> CompleteEvent(
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

        var item = await _context.DetectedWorkEvents
            .FirstOrDefaultAsync(
                x => x.DetectedWorkEventId == id && x.UserId == userId,
                cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đề xuất công việc."
            });
        }

        if (item.Status != "Confirmed")
        {
            return Conflict(new
            {
                message = "Chỉ có thể hoàn thành công việc đã được xác nhận.",
                currentStatus = item.Status
            });
        }

        item.Status = "Completed";

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã đánh dấu công việc hoàn thành.",
            id = item.DetectedWorkEventId,
            status = item.Status
        });
    }
}
