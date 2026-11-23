using System.Security.Claims;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Email;

namespace OfficeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class WorkEventImportController : ControllerBase
{
    private static readonly string[] AllowedTypes =
        { "Meeting", "Deadline", "Appointment", "Task", "Other" };

    private readonly ApplicationDbContext _db;
    private readonly IAIService _ai;
    private readonly ILogger<WorkEventImportController> _logger;

    public WorkEventImportController(
        ApplicationDbContext db, IAIService ai,
        ILogger<WorkEventImportController> logger)
    {
        _db = db; _ai = ai; _logger = logger;
    }

    // POST api/WorkEventImport/preview  (form-data: file = .docx)
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Preview(
        [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file Word." });

        if (!Path.GetExtension(file.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ hỗ trợ file .docx." });

        string text;
        try
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            text = ReadDocx(ms);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OpenXmlPackageException)
        {
            return BadRequest(new { message = "Không đọc được file. File có thể bị lỗi hoặc đặt mật khẩu." });
        }

        text = text.Trim();
        if (text.Length < 20)
            return BadRequest(new { message = "File không có đủ nội dung để trích xuất lịch." });

        try
        {
            var todayVn = DateTime.UtcNow.AddHours(7);
            var found = await _ai.ExtractEventsFromDocumentAsync(
                Path.GetFileNameWithoutExtension(file.FileName), text, todayVn, cancellationToken);

            var events = found
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Take(50)
                .Select(x => new
                {
                    title = x.Title.Trim(),
                    description = x.Description?.Trim(),
                    eventType = AllowedTypes.FirstOrDefault(t =>
                        string.Equals(t, x.EventType, StringComparison.OrdinalIgnoreCase)) ?? "Other",
                    startAtUtc = GmailWorkEventScanner.ParseUtc(x.StartAt),
                    dueAtUtc = GmailWorkEventScanner.ParseUtc(x.DueAt)
                })
                .ToList();

            return Ok(new { fileName = file.FileName, events });
        }
        catch (GeminiQuotaException ex)
        {
            return StatusCode(429, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = ex.RetryAfterUtc
            });
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(ex, "Lỗi khi AI trích lịch từ Word.");
            return StatusCode(500, new { message = "Không thể trích xuất lịch từ file." });
        }
    }

    // POST api/WorkEventImport/confirm
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        ImportConfirmRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        if (request.Events == null || request.Events.Count == 0 || request.Events.Count > 100)
            return BadRequest(new { message = "Danh sách sự kiện không hợp lệ (1–100 mục)." });

        var now = DateTime.UtcNow;
        var items = new List<Models.DetectedWorkEvent>();

        foreach (var e in request.Events)
        {
            var title = e.Title?.Trim() ?? "";

            if (title.Length == 0 || title.Length > 300)
                return BadRequest(new { message = "Tiêu đề không được để trống (tối đa 300 ký tự)." });

            if (!e.StartAt.HasValue && !e.DueAt.HasValue)
                return BadRequest(new { message = $"\"{title}\": cần có thời gian bắt đầu hoặc hạn chót." });

            if (e.StartAt.HasValue && e.DueAt.HasValue && e.DueAt < e.StartAt)
                return BadRequest(new { message = $"\"{title}\": hạn chót phải sau thời gian bắt đầu." });

            items.Add(new Models.DetectedWorkEvent
            {
                UserId = userId,
                GmailMessageId = "manual-" + Guid.NewGuid().ToString("N"),
                EventType = AllowedTypes.FirstOrDefault(t =>
                    string.Equals(t, e.EventType, StringComparison.OrdinalIgnoreCase)) ?? "Other",
                Title = title,
                Description = string.IsNullOrWhiteSpace(e.Description)
                    ? null
                    : (e.Description.Trim().Length > 4000 ? e.Description.Trim()[..4000] : e.Description.Trim()),
                StartAtUtc = e.StartAt?.ToUniversalTime(),
                DueAtUtc = e.DueAt?.ToUniversalTime(),
                TimeZoneId = "Asia/Ho_Chi_Minh",
                Status = "Confirmed",
                DetectedAtUtc = now,
                ConfirmedAtUtc = now
            });
        }

        _db.DetectedWorkEvents.AddRange(items);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Đã thêm {items.Count} công việc vào lịch.", count = items.Count });
    }

    // Đọc cả đoạn văn lẫn bảng (mỗi dòng bảng nối bằng " | ") vì lịch trong Word thường là bảng.
    private static string ReadDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        var sb = new StringBuilder();

        foreach (var element in body.Elements())
        {
            if (element is Paragraph p)
            {
                if (!string.IsNullOrWhiteSpace(p.InnerText)) sb.AppendLine(p.InnerText);
            }
            else if (element is Table table)
            {
                foreach (var row in table.Elements<TableRow>())
                {
                    var cells = row.Elements<TableCell>()
                        .Select(c => c.InnerText.Trim().Replace("\n", " "));
                    sb.AppendLine(string.Join(" | ", cells));
                }
            }
        }

        return sb.ToString();
    }
}

public sealed class ImportConfirmRequest
{
    public List<ManualEventRequest> Events { get; set; } = new();
}