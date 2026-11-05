using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Files;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Tóm tắt văn bản thành các ý chính bằng AI.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SummaryController : ControllerBase
{
    private const int MaxChars = 12000;
    private const int MinChars = 50;

    private readonly IAIService _ai;
    private readonly ApplicationDbContext _db;
    private readonly LocalFileSearchService _files;
    private readonly ILogger<SummaryController> _logger;

    public SummaryController(
        IAIService ai,
        ApplicationDbContext db,
        LocalFileSearchService files,
        ILogger<SummaryController> logger)
    {
        _ai = ai;
        _db = db;
        _files = files;
        _logger = logger;
    }

    // POST api/Summary/text  (văn bản chưa lưu, ví dụ nội dung AI vừa tạo)
    [HttpPost("text")]
    public Task<IActionResult> SummarizeText(
        SummarizeTextRequest request, CancellationToken cancellationToken) =>
        RunAsync(request.Title, request.Content, cancellationToken);

    // POST api/Summary/document/5  (văn bản đã lưu của người dùng)
    [HttpPost("document/{id:int}")]
    public async Task<IActionResult> SummarizeDocument(
        int id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var document = await _db.Documents
            .AsNoTracking()
            .Where(x => x.DocumentId == id && x.UserId == userId)
            .Select(x => new { x.Title, x.Content })
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy văn bản." });
        }

        return await RunAsync(
            document.Title, document.Content, cancellationToken);
    }

    // POST api/Summary/file  (file tìm được trong thư mục cấu hình)
    [HttpPost("file")]
    public async Task<IActionResult> SummarizeFile(
        SummarizeFileRequest request, CancellationToken cancellationToken)
    {
        var path = _files.ResolvePath(request.RootIndex, request.RelativePath);

        if (path == null)
        {
            return NotFound(new { message = "Không tìm thấy file." });
        }

        if (!LocalFileSearchService.IsSummarizable(path))
        {
            return BadRequest(new
            {
                message = "Hiện chỉ tóm tắt được file .docx, .txt, .md, .csv."
            });
        }

        string text;

        try
        {
            text = await Task.Run(
                () => _files.ExtractText(path), cancellationToken);
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            _logger.LogWarning(ex, "Không đọc được file để tóm tắt.");

            return BadRequest(new
            {
                message = "Không đọc được file. File có thể bị lỗi hoặc đặt mật khẩu."
            });
        }

        return await RunAsync(
            Path.GetFileNameWithoutExtension(path), text, cancellationToken);
    }

    // ---------------------------------------------------------
    private async Task<IActionResult> RunAsync(
        string? title, string? content, CancellationToken cancellationToken)
    {
        var text = content?.Trim() ?? string.Empty;

        if (text.Length < MinChars)
        {
            return BadRequest(new
            {
                message = "Nội dung quá ngắn hoặc trống, không cần tóm tắt."
            });
        }

        var truncated = text.Length > MaxChars;

        if (truncated)
        {
            text = text[..MaxChars];
        }

        try
        {
            var summary = await _ai.SummarizeDocumentAsync(
                title ?? string.Empty, text, cancellationToken);

            return Ok(new { summary, truncated });
        }
        catch (GeminiQuotaException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = ex.RetryAfterUtc
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi AI tóm tắt văn bản.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Đã xảy ra lỗi khi nhờ AI tóm tắt."
            });
        }
    }
}

public sealed class SummarizeTextRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class SummarizeFileRequest
{
    public int RootIndex { get; set; }
    public string RelativePath { get; set; } = string.Empty;
}