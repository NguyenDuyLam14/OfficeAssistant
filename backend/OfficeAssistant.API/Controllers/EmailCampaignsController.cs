using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs.Email;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.Email;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Gửi email hàng loạt: nhập người nhận từ Excel/CSV, xem trước,
/// tạo chiến dịch, bắt đầu/hủy và theo dõi tiến độ.
/// Việc gửi thực tế do EmailCampaignBackgroundService xử lý.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class EmailCampaignsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly int _maxRecipients;

    public EmailCampaignsController(
        ApplicationDbContext db,
        IConfiguration configuration)
    {
        _db = db;

        _maxRecipients = Math.Max(
            1, configuration.GetValue("EmailCampaign:MaxRecipients", 500));
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out userId) && userId > 0;
    }

    // =========================================================
    // POST api/EmailCampaigns/parse-recipients
    // Form-data: file = .xlsx / .xlsm / .csv
    // =========================================================
    [HttpPost("parse-recipients")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public IActionResult ParseRecipients([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file." });
        }

        var extension = Path.GetExtension(file.FileName);

        var allowed =
            extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

        if (!allowed)
        {
            return BadRequest(new
            {
                message = "Chỉ hỗ trợ file .xlsx hoặc .csv."
            });
        }

        try
        {
            using var stream = file.OpenReadStream();

            return Ok(RecipientFileParser.Parse(
                stream, extension, _maxRecipients));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return BadRequest(new
            {
                message = "Không đọc được file. File có thể bị lỗi hoặc đặt mật khẩu."
            });
        }
    }

    // =========================================================
    // POST api/EmailCampaigns/preview
    // =========================================================
    [HttpPost("preview")]
    public IActionResult Preview(PreviewCampaignRequest request)
    {
        var recipient = request.Recipient;

        var values = EmailTemplateRenderer.BuildValues(
            recipient?.Email ?? string.Empty,
            recipient?.Name,
            recipient == null
                ? null
                : JsonSerializer.Serialize(recipient.Fields));

        return Ok(new
        {
            subject = GmailSender.SingleLine(
                EmailTemplateRenderer.Render(request.Subject, values), 300),
            body = EmailTemplateRenderer.Render(request.BodyTemplate, values)
        });
    }

    // =========================================================
    // POST api/EmailCampaigns  (tạo bản nháp)
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var subject = request.Subject?.Trim() ?? string.Empty;
        var body = request.BodyTemplate?.Trim() ?? string.Empty;

        if (name.Length == 0 || name.Length > 200)
        {
            return BadRequest(new
            {
                message = "Tên chiến dịch không được để trống (tối đa 200 ký tự)."
            });
        }

        if (subject.Length == 0 || subject.Length > 300)
        {
            return BadRequest(new
            {
                message = "Tiêu đề thư không được để trống (tối đa 300 ký tự)."
            });
        }

        if (body.Length == 0 || body.Length > 20000)
        {
            return BadRequest(new
            {
                message = "Nội dung thư không được để trống (tối đa 20.000 ký tự)."
            });
        }

        if (request.Recipients == null || request.Recipients.Count == 0)
        {
            return BadRequest(new
            {
                message = "Chiến dịch cần ít nhất một người nhận."
            });
        }

        if (request.Recipients.Count > _maxRecipients)
        {
            return BadRequest(new
            {
                message = $"Tối đa {_maxRecipients} người nhận mỗi chiến dịch."
            });
        }

        // Chuẩn hóa và kiểm tra lại người nhận (không tin dữ liệu từ client).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recipients = new List<EmailCampaignRecipient>();
        var knownVariables = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase) { "Email", "Ten" };

        foreach (var input in request.Recipients)
        {
            var email = input.Email?.Trim();

            if (!RecipientFileParser.IsValidEmail(email))
            {
                return BadRequest(new
                {
                    message = $"Địa chỉ email không hợp lệ: {input.Email}"
                });
            }

            if (!seen.Add(email!))
            {
                continue;
            }

            var fields = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in (input.Fields ?? new()).Take(50))
            {
                var key = item.Key?.Trim();

                if (string.IsNullOrEmpty(key) || key.Length > 100)
                {
                    continue;
                }

                var value = item.Value ?? string.Empty;
                fields[key] = value.Length > 500 ? value[..500] : value;
                knownVariables.Add(key);
            }

            var recipientName = input.Name?.Trim();

            if (recipientName is { Length: > 200 })
            {
                recipientName = recipientName[..200];
            }

            recipients.Add(new EmailCampaignRecipient
            {
                Email = email!,
                Name = string.IsNullOrWhiteSpace(recipientName)
                    ? null
                    : recipientName,
                DataJson = JsonSerializer.Serialize(fields),
                Status = "Pending"
            });
        }

        // Mọi biến {{...}} trong mẫu phải tồn tại trong dữ liệu.
        var unknown = EmailTemplateRenderer.ExtractVariables(subject)
            .Concat(EmailTemplateRenderer.ExtractVariables(body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(variable => !knownVariables.Contains(variable))
            .ToList();

        if (unknown.Count > 0)
        {
            return BadRequest(new
            {
                message = "Biến không có trong dữ liệu người nhận: " +
                          string.Join(", ", unknown.Select(u => "{{" + u + "}}"))
            });
        }

        var campaign = new EmailCampaign
        {
            UserId = userId,
            Name = name,
            Subject = subject,
            BodyTemplate = body,
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow,
            Recipients = recipients
        };

        _db.EmailCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = campaign.EmailCampaignId },
            new
            {
                id = campaign.EmailCampaignId,
                status = campaign.Status,
                total = recipients.Count
            });
    }

    // =========================================================
    // POST api/EmailCampaigns/{id}/start
    // =========================================================
    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(
        int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var campaign = await _db.EmailCampaigns.FirstOrDefaultAsync(
            x => x.EmailCampaignId == id && x.UserId == userId,
            cancellationToken);

        if (campaign == null)
        {
            return NotFound(new { message = "Không tìm thấy chiến dịch." });
        }

        if (campaign.Status != "Draft")
        {
            return Conflict(new
            {
                message = "Chỉ có thể bắt đầu chiến dịch ở trạng thái bản nháp.",
                currentStatus = campaign.Status
            });
        }

        var hasGmail = await _db.GmailConnections.AnyAsync(
            x => x.UserId == userId, cancellationToken);

        if (!hasGmail)
        {
            return Conflict(new
            {
                code = "gmail_reconnect_required",
                message = "Bạn chưa kết nối Gmail. Hãy kết nối Gmail trước khi gửi."
            });
        }

        campaign.Status = "Queued";
        campaign.Note = null;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Chiến dịch đã được xếp hàng và sẽ gửi dần trong nền.",
            id = campaign.EmailCampaignId,
            status = campaign.Status
        });
    }

    // =========================================================
    // POST api/EmailCampaigns/{id}/cancel
    // =========================================================
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var campaign = await _db.EmailCampaigns.FirstOrDefaultAsync(
            x => x.EmailCampaignId == id && x.UserId == userId,
            cancellationToken);

        if (campaign == null)
        {
            return NotFound(new { message = "Không tìm thấy chiến dịch." });
        }

        if (campaign.Status is not ("Draft" or "Queued" or "Running"))
        {
            return Conflict(new
            {
                message = "Chiến dịch đã kết thúc, không thể hủy.",
                currentStatus = campaign.Status
            });
        }

        campaign.Status = "Cancelled";
        campaign.FinishedAtUtc = DateTime.UtcNow;
        campaign.Note = "Đã hủy bởi người dùng.";

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đã hủy chiến dịch.", status = campaign.Status });
    }

    // =========================================================
    // DELETE api/EmailCampaigns/{id}
    // =========================================================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var campaign = await _db.EmailCampaigns.FirstOrDefaultAsync(
            x => x.EmailCampaignId == id && x.UserId == userId,
            cancellationToken);

        if (campaign == null)
        {
            return NotFound(new { message = "Không tìm thấy chiến dịch." });
        }

        if (campaign.Status is "Queued" or "Running")
        {
            return Conflict(new
            {
                message = "Hãy hủy chiến dịch đang chạy trước khi xóa."
            });
        }

        _db.EmailCampaigns.Remove(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đã xóa chiến dịch." });
    }

    // =========================================================
    // GET api/EmailCampaigns
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await _db.EmailCampaigns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(x => new
            {
                id = x.EmailCampaignId,
                name = x.Name,
                subject = x.Subject,
                status = x.Status,
                note = x.Note,
                total = x.Recipients.Count,
                sent = x.Recipients.Count(r => r.Status == "Sent"),
                failed = x.Recipients.Count(r => r.Status == "Failed"),
                pending = x.Recipients.Count(r => r.Status == "Pending"),
                createdAtUtc = x.CreatedAtUtc,
                startedAtUtc = x.StartedAtUtc,
                finishedAtUtc = x.FinishedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    // =========================================================
    // GET api/EmailCampaigns/{id}
    // =========================================================
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var campaign = await _db.EmailCampaigns
            .AsNoTracking()
            .Where(x => x.EmailCampaignId == id && x.UserId == userId)
            .Select(x => new
            {
                id = x.EmailCampaignId,
                name = x.Name,
                subject = x.Subject,
                status = x.Status,
                note = x.Note,
                createdAtUtc = x.CreatedAtUtc,
                startedAtUtc = x.StartedAtUtc,
                finishedAtUtc = x.FinishedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign == null)
        {
            return NotFound(new { message = "Không tìm thấy chiến dịch." });
        }

        var counts = await _db.EmailCampaignRecipients
            .AsNoTracking()
            .Where(x => x.EmailCampaignId == id)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Count(string status) =>
            counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        // Hiển thị người nhận lỗi trước, rồi đang chờ, rồi đã gửi.
        var recipients = await _db.EmailCampaignRecipients
            .AsNoTracking()
            .Where(x => x.EmailCampaignId == id)
            .OrderBy(x => x.Status == "Failed" ? 0
                        : x.Status == "Pending" ? 1 : 2)
            .ThenBy(x => x.EmailCampaignRecipientId)
            .Take(300)
            .Select(x => new
            {
                id = x.EmailCampaignRecipientId,
                email = x.Email,
                name = x.Name,
                status = x.Status,
                error = x.Error,
                sentAtUtc = x.SentAtUtc
            })
            .ToListAsync(cancellationToken);

        var sent = Count("Sent");
        var failed = Count("Failed");
        var pending = Count("Pending");

        return Ok(new
        {
            campaign.id,
            campaign.name,
            campaign.subject,
            campaign.status,
            campaign.note,
            total = sent + failed + pending,
            sent,
            failed,
            pending,
            campaign.createdAtUtc,
            campaign.startedAtUtc,
            campaign.finishedAtUtc,
            recipients
        });
    }
}
