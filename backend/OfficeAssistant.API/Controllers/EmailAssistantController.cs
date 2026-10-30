using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs.Email;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Email;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Trợ lý email: tóm tắt, gợi ý trả lời và gửi thư đơn.
/// Dùng chung route api/Emails với các controller Gmail hiện có.
/// </summary>
[ApiController]
[Route("api/Emails")]
[Authorize]
public sealed class EmailAssistantController : ControllerBase
{
    private static readonly HashSet<string> Tones =
        new() { "formal", "friendly", "short" };

    private readonly ApplicationDbContext _db;
    private readonly GmailMessageReader _reader;
    private readonly GmailSender _sender;
    private readonly IAIService _ai;
    private readonly ILogger<EmailAssistantController> _logger;

    public EmailAssistantController(
        ApplicationDbContext db,
        GmailMessageReader reader,
        GmailSender sender,
        IAIService ai,
        ILogger<EmailAssistantController> logger)
    {
        _db = db;
        _reader = reader;
        _sender = sender;
        _ai = ai;
        _logger = logger;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out userId) && userId > 0;
    }

    private static bool IsValidMessageId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 200 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    // =========================================================
    // POST api/Emails/messages/{id}/summarize?refresh=false
    // =========================================================
    [HttpPost("messages/{messageId}/summarize")]
    public async Task<IActionResult> Summarize(
        string messageId,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        if (!IsValidMessageId(messageId))
        {
            return BadRequest(new { message = "Mã email không hợp lệ." });
        }

        // Có sẵn bản tóm tắt thì trả luôn, không tốn quota AI.
        var cached = await _db.EmailSummaryCaches
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.GmailMessageId == messageId,
                cancellationToken);

        if (cached != null && !refresh)
        {
            return Ok(new { summary = cached.Summary, cached = true });
        }

        var (email, error) = await LoadEmailAsync(
            userId, messageId, cancellationToken);

        if (error != null)
        {
            return error;
        }

        string summary;

        try
        {
            summary = await _ai.SummarizeEmailAsync(
                email!.Subject,
                email.Sender,
                email.Date,
                email.Body,
                cancellationToken);
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException &&
              cancellationToken.IsCancellationRequested))
        {
            return AiError(ex);
        }

        summary = summary.Length > 4000 ? summary[..4000] : summary;

        try
        {
            if (cached == null)
            {
                _db.EmailSummaryCaches.Add(new EmailSummaryCache
                {
                    UserId = userId,
                    GmailMessageId = messageId,
                    Summary = summary
                });
            }
            else
            {
                cached.Summary = summary;
                cached.CreatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Lưu cache lỗi (ví dụ hai yêu cầu cùng lúc) không được
            // làm mất kết quả đã tạo.
            _logger.LogWarning(ex, "Không lưu được cache tóm tắt email.");
            _db.ChangeTracker.Clear();
        }

        return Ok(new { summary, cached = false });
    }

    // =========================================================
    // POST api/Emails/messages/{id}/suggest-reply
    // =========================================================
    [HttpPost("messages/{messageId}/suggest-reply")]
    public async Task<IActionResult> SuggestReply(
        string messageId,
        SuggestReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        if (!IsValidMessageId(messageId))
        {
            return BadRequest(new { message = "Mã email không hợp lệ." });
        }

        var tone = Tones.Contains(request.Tone ?? string.Empty)
            ? request.Tone!
            : "formal";

        var (email, error) = await LoadEmailAsync(
            userId, messageId, cancellationToken);

        if (error != null)
        {
            return error;
        }

        try
        {
            var reply = await _ai.SuggestReplyAsync(
                email!.Subject,
                email.Sender,
                email.Date,
                email.Body,
                tone,
                request.Note,
                cancellationToken);

            return Ok(new { reply });
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException &&
              cancellationToken.IsCancellationRequested))
        {
            return AiError(ex);
        }
    }

    // =========================================================
    // POST api/Emails/send
    // =========================================================
    [HttpPost("send")]
    public async Task<IActionResult> Send(
        SendEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        if (request.Body is { Length: > 50000 })
        {
            return BadRequest(new
            {
                message = "Nội dung thư quá dài (tối đa 50.000 ký tự)."
            });
        }

        try
        {
            var id = await _sender.SendAsync(
                userId,
                new GmailSendRequest(
                    request.To,
                    request.Cc,
                    request.Subject,
                    request.Body,
                    request.ReplyToMessageId),
                cancellationToken);

            return Ok(new { message = "Đã gửi thư.", id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (GmailSendException ex)
        {
            return ex.StatusCode switch
            {
                401 or 403 => ReconnectRequired(ex.Message),
                404 => NotFound(new { message = ex.Message }),
                429 => StatusCode(429, new { message = ex.Message }),
                400 => BadRequest(new { message = ex.Message }),
                _ => StatusCode(502, new { message = ex.Message })
            };
        }
        catch (InvalidOperationException ex)
        {
            // Chưa kết nối Gmail hoặc không làm mới được token.
            _logger.LogWarning(ex, "Gửi thư thất bại vì kết nối Gmail.");

            return ReconnectRequired(
                "Kết nối Gmail không còn hiệu lực. Vui lòng kết nối lại Gmail.");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                message = "Không thể kết nối đến dịch vụ Gmail."
            });
        }
    }

    // =========================================================
    // HÀM HỖ TRỢ
    // =========================================================

    /// <summary>
    /// Đọc email và trích nội dung văn bản.
    /// Email chỉ có HTML thì dùng đoạn xem trước (snippet) của Gmail.
    /// </summary>
    private async Task<(GmailEmailContent? Email, IActionResult? Error)>
        LoadEmailAsync(
            int userId,
            string messageId,
            CancellationToken cancellationToken)
    {
        try
        {
            using var document = await _reader.GetFullMessageAsync(
                userId, messageId, cancellationToken);

            var email = new GmailEmailContentExtractor().Extract(document);

            if (string.IsNullOrWhiteSpace(email.Body) &&
                document.RootElement.TryGetProperty(
                    "snippet", out var snippet) &&
                snippet.ValueKind == JsonValueKind.String)
            {
                email.Body = snippet.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(email.Subject) &&
                string.IsNullOrWhiteSpace(email.Body))
            {
                return (null, BadRequest(new
                {
                    message = "Email không có nội dung văn bản để xử lý."
                }));
            }

            return (email, null);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, NotFound(new
            {
                message = "Không tìm thấy email trong Gmail đã kết nối."
            }));
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized ||
                  ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return (null, ReconnectRequired(
                "Quyền truy cập Gmail không còn hiệu lực. Vui lòng kết nối lại Gmail."));
        }
        catch (HttpRequestException)
        {
            return (null, StatusCode(502, new
            {
                message = "Không thể kết nối đến dịch vụ Gmail."
            }));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Không đọc được email vì kết nối Gmail.");

            return (null, ReconnectRequired(
                "Kết nối Gmail không còn hiệu lực. Vui lòng kết nối lại Gmail."));
        }
    }

    private IActionResult AiError(Exception ex)
    {
        if (ex is GeminiQuotaException quota)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = quota.RetryAfterUtc
            });
        }

        _logger.LogError(ex, "Lỗi khi gọi AI xử lý email.");

        return StatusCode(StatusCodes.Status500InternalServerError, new
        {
            message = "Đã xảy ra lỗi khi xử lý yêu cầu AI."
        });
    }

    // 409 thay vì 401 để frontend không nhầm với JWT hết hạn.
    private IActionResult ReconnectRequired(string message) =>
        Conflict(new { code = "gmail_reconnect_required", message });
}
