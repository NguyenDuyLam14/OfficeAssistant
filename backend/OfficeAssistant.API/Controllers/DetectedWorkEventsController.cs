
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;

using OfficeAssistant.API.Services.Email;

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

    private const string ManualPrefix = "manual-";

    private static readonly string[] AllowedEventTypes =
        { "Meeting", "Deadline", "Appointment", "Task", "Other" };

    private static string? ValidateManual(
        ManualEventRequest request, out string eventType)
    {
        eventType = AllowedEventTypes.FirstOrDefault(x =>
            string.Equals(x, request.EventType,
                StringComparison.OrdinalIgnoreCase)) ?? "Other";

        var title = request.Title?.Trim() ?? string.Empty;

        if (title.Length == 0 || title.Length > 300)
        {
            return "Tiêu đề không được để trống (tối đa 300 ký tự).";
        }

        if (request.Description is { Length: > 4000 })
        {
            return "Mô tả tối đa 4000 ký tự.";
        }

        if (!request.StartAt.HasValue && !request.DueAt.HasValue)
        {
            return "Cần chọn thời gian bắt đầu hoặc hạn chót.";
        }

        if (request.StartAt.HasValue && request.DueAt.HasValue &&
            request.DueAt < request.StartAt)
        {
            return "Hạn chót phải sau thời gian bắt đầu.";
        }

        return null;
    }

    /// <summary>
    /// POST /api/DetectedWorkEvents/manual
    /// Người dùng tự tạo công việc, vào lịch ngay (đã xác nhận).
    /// </summary>
    [HttpPost("manual")]
    public async Task<IActionResult> CreateManualEvent(
        ManualEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        var error = ValidateManual(request, out var eventType);

        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        var now = DateTime.UtcNow;

        var item = new DetectedWorkEvent
        {
            UserId = userId,
            GmailMessageId = ManualPrefix + Guid.NewGuid().ToString("N"),
            EventType = eventType,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            StartAtUtc = request.StartAt?.ToUniversalTime(),
            DueAtUtc = request.DueAt?.ToUniversalTime(),
            TimeZoneId = "Asia/Ho_Chi_Minh",
            Status = "Confirmed",
            DetectedAtUtc = now,
            ConfirmedAtUtc = now
        };

        _context.DetectedWorkEvents.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã thêm công việc.",
            id = item.DetectedWorkEventId
        });
    }

    /// <summary>
    /// PUT /api/DetectedWorkEvents/{id}/manual
    /// Chỉ sửa được công việc do người dùng tự tạo.
    /// </summary>
    [HttpPut("{id:int}/manual")]
    public async Task<IActionResult> UpdateManualEvent(
        int id,
        ManualEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        var error = ValidateManual(request, out var eventType);

        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        var item = await _context.DetectedWorkEvents.FirstOrDefaultAsync(
            x => x.DetectedWorkEventId == id
                 && x.UserId == userId
                 && x.GmailMessageId.StartsWith(ManualPrefix),
            cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy công việc tự tạo."
            });
        }

        item.EventType = eventType;
        item.Title = request.Title.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        item.StartAtUtc = request.StartAt?.ToUniversalTime();
        item.DueAtUtc = request.DueAt?.ToUniversalTime();

        // Đổi giờ thì nhắc việc phải được gửi lại theo giờ mới.
        var oldReminders = await _context.AppNotifications
            .Where(n => n.DetectedWorkEventId == id
                        && (n.NotificationType ==
                                WorkEventReminderBackgroundService.Type24h
                            || n.NotificationType ==
                                WorkEventReminderBackgroundService.Type1h))
            .ToListAsync(cancellationToken);

        _context.AppNotifications.RemoveRange(oldReminders);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đã cập nhật công việc.", id });
    }

    /// <summary>
    /// DELETE /api/DetectedWorkEvents/{id}
    /// Chỉ xóa được công việc do người dùng tự tạo.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteManualEvent(
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

        var item = await _context.DetectedWorkEvents.FirstOrDefaultAsync(
            x => x.DetectedWorkEventId == id
                 && x.UserId == userId
                 && x.GmailMessageId.StartsWith(ManualPrefix),
            cancellationToken);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy công việc tự tạo."
            });
        }

        var reminders = await _context.AppNotifications
            .Where(n => n.DetectedWorkEventId == id)
            .ToListAsync(cancellationToken);

        _context.AppNotifications.RemoveRange(reminders);
        _context.DetectedWorkEvents.Remove(item);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đã xóa công việc." });
    }

    /// <summary>
    /// GET /api/DetectedWorkEvents/calendar?from=...&to=...
    /// Lấy các công việc đã xác nhận/hoàn thành trong khoảng thời gian
    /// để hiển thị lên lịch. Mốc thời gian dùng StartAtUtc, nếu không có thì DueAtUtc.
    /// </summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendarEvents(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        if (to <= from || (to - from).TotalDays > 62)
        {
            return BadRequest(new
            {
                message = "Khoảng thời gian không hợp lệ (tối đa 62 ngày)."
            });
        }

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var items = await _context.DetectedWorkEvents
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && (x.Status == "Confirmed" || x.Status == "Completed")
                && (
                    (x.StartAtUtc != null && x.StartAtUtc >= fromUtc && x.StartAtUtc < toUtc)
                    || (x.StartAtUtc == null && x.DueAtUtc != null
                        && x.DueAtUtc >= fromUtc && x.DueAtUtc < toUtc)))
            .OrderBy(x => x.StartAtUtc ?? x.DueAtUtc)
            .Select(x => new
            {
                id = x.DetectedWorkEventId,
                title = x.Title,
                description = x.Description,
                eventType = x.EventType,
                startAtUtc = x.StartAtUtc,
                dueAtUtc = x.DueAtUtc,
                status = x.Status,
                isManual = x.GmailMessageId.StartsWith(ManualPrefix)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
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

public sealed class ManualEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "Task";
    public DateTime? StartAt { get; set; }
    public DateTime? DueAt { get; set; }
}
