using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Files;

using DocumentFormat.OpenXml.Packaging;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Tóm tắt văn bản và hỏi đáp trên văn bản bằng AI.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SummaryController : ControllerBase
{
    private const int MaxChars = 12000;
    private const int MinChars = 50;
    private const int MaxQuestionChars = 500;

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

    private bool TryGetUserId(out int userId) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    // =========================================================
    // TÓM TẮT
    // =========================================================

    // POST api/Summary/text
    [HttpPost("text")]
    public Task<IActionResult> SummarizeText(
        SummarizeTextRequest request, CancellationToken cancellationToken) =>
        SummarizeAsync(request.Title, request.Content, cancellationToken);

    // POST api/Summary/document/5
    [HttpPost("document/{id:int}")]
    public async Task<IActionResult> SummarizeDocument(
        int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var document = await FindDocumentAsync(id, userId, cancellationToken);

        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy văn bản." });
        }

        return await SummarizeAsync(
            document.Title, document.Content, cancellationToken);
    }

    // POST api/Summary/file
    [HttpPost("file")]
    public async Task<IActionResult> SummarizeFile(
        SummarizeFileRequest request, CancellationToken cancellationToken)
    {
        var (title, text, error) = await ReadFileAsync(
            request.RootIndex, request.RelativePath, cancellationToken);

        if (error != null)
        {
            return error;
        }

        return await SummarizeAsync(title, text, cancellationToken);
    }

    // POST api/Summary/upload  (form-data: file)
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> SummarizeUpload(
        [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "File không được vượt quá 10 MB." });

        if (!LocalFileSearchService.IsSummarizable(file.FileName))
            return BadRequest(new
            {
                message = "Hiện chỉ hỗ trợ file .docx, .txt, .md, .csv."
            });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var temp = Path.Combine(Path.GetTempPath(), $"sum_{Guid.NewGuid():N}{ext}");
        string text;

        try
        {
            await using (var fs = new FileStream(temp, FileMode.Create))
            {
                await file.CopyToAsync(fs, cancellationToken);
            }

            text = await Task.Run(() => _files.ExtractText(temp), cancellationToken);
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or OpenXmlPackageException)
        {
            _logger.LogWarning(ex, "Không đọc được file upload.");
            return BadRequest(new
            {
                message = "Không đọc được file. File có thể bị lỗi hoặc đặt mật khẩu."
            });
        }
        finally
        {
            try { System.IO.File.Delete(temp); } catch { }
        }

        var prepared = PrepareText(text, out var truncated);

        if (prepared == null)
            return BadRequest(new
            {
                message = "Nội dung quá ngắn hoặc trống, không cần tóm tắt."
            });

        try
        {
            var summary = await _ai.SummarizeDocumentAsync(
                Path.GetFileNameWithoutExtension(file.FileName),
                prepared, cancellationToken);

            return Ok(new
            {
                summary,
                truncated,
                fileName = file.FileName,
                text = prepared   // dùng cho "PPT từ nội dung gốc"
            });
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return AiFailure(ex);
        }
    }

    // =========================================================
    // HỎI ĐÁP
    // =========================================================

    // POST api/Summary/ask-text
    [HttpPost("ask-text")]
    public Task<IActionResult> AskText(
        AskTextRequest request, CancellationToken cancellationToken) =>
        AskAsync(
            request.Title, request.Content, request.Question,
            cancellationToken);

    // POST api/Summary/ask-document/5
    [HttpPost("ask-document/{id:int}")]
    public async Task<IActionResult> AskDocument(
        int id, AskDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var document = await FindDocumentAsync(id, userId, cancellationToken);

        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy văn bản." });
        }

        return await AskAsync(
            document.Title, document.Content, request.Question,
            cancellationToken);
    }

    // POST api/Summary/ask-file
    [HttpPost("ask-file")]
    public async Task<IActionResult> AskFile(
        AskFileRequest request, CancellationToken cancellationToken)
    {
        var (title, text, error) = await ReadFileAsync(
            request.RootIndex, request.RelativePath, cancellationToken);

        if (error != null)
        {
            return error;
        }

        return await AskAsync(
            title, text, request.Question, cancellationToken);
    }

    // =========================================================
    // HÀM HỖ TRỢ
    // =========================================================

    private Task<DocumentBrief?> FindDocumentAsync(
        int id, int userId, CancellationToken cancellationToken) =>
        _db.Documents
            .AsNoTracking()
            .Where(x => x.DocumentId == id && x.UserId == userId)
            .Select(x => new DocumentBrief(x.Title, x.Content))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<(string Title, string Text, IActionResult? Error)>
        ReadFileAsync(
            int rootIndex,
            string relativePath,
            CancellationToken cancellationToken)
    {
        var path = _files.ResolvePath(rootIndex, relativePath);

        if (path == null)
        {
            return (string.Empty, string.Empty,
                NotFound(new { message = "Không tìm thấy file." }));
        }

        if (!LocalFileSearchService.IsSummarizable(path))
        {
            return (string.Empty, string.Empty, BadRequest(new
            {
                message = "Hiện chỉ hỗ trợ file .docx, .txt, .md, .csv."
            }));
        }

        try
        {
            var text = await Task.Run(
                () => _files.ExtractText(path), cancellationToken);

            return (Path.GetFileNameWithoutExtension(path), text, null);
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            _logger.LogWarning(ex, "Không đọc được file.");

            return (string.Empty, string.Empty, BadRequest(new
            {
                message = "Không đọc được file. File có thể bị lỗi hoặc đặt mật khẩu."
            }));
        }
    }

    // Cắt về giới hạn; trả null nếu nội dung quá ngắn.
    private static string? PrepareText(string? content, out bool truncated)
    {
        var text = content?.Trim() ?? string.Empty;
        truncated = false;

        if (text.Length < MinChars)
        {
            return null;
        }

        if (text.Length > MaxChars)
        {
            text = text[..MaxChars];
            truncated = true;
        }

        return text;
    }

    private async Task<IActionResult> SummarizeAsync(
        string? title, string? content, CancellationToken cancellationToken)
    {
        var text = PrepareText(content, out var truncated);

        if (text == null)
        {
            return BadRequest(new
            {
                message = "Nội dung quá ngắn hoặc trống, không cần tóm tắt."
            });
        }

        try
        {
            var summary = await _ai.SummarizeDocumentAsync(
                title ?? string.Empty, text, cancellationToken);

            return Ok(new { summary, truncated });
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException &&
              cancellationToken.IsCancellationRequested))
        {
            return AiFailure(ex);
        }
    }

    private async Task<IActionResult> AskAsync(
        string? title,
        string? content,
        string? question,
        CancellationToken cancellationToken)
    {
        var q = question?.Trim() ?? string.Empty;

        if (q.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng nhập câu hỏi." });
        }

        if (q.Length > MaxQuestionChars)
        {
            return BadRequest(new
            {
                message = $"Câu hỏi tối đa {MaxQuestionChars} ký tự."
            });
        }

        var text = PrepareText(content, out var truncated);

        if (text == null)
        {
            return BadRequest(new
            {
                message = "Nội dung quá ngắn hoặc trống để hỏi đáp."
            });
        }

        try
        {
            var answer = await _ai.AnswerDocumentQuestionAsync(
                title ?? string.Empty, text, q, cancellationToken);

            return Ok(new { answer, truncated });
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException &&
              cancellationToken.IsCancellationRequested))
        {
            return AiFailure(ex);
        }
    }

    private IActionResult AiFailure(Exception ex)
    {
        if (ex is GeminiQuotaException quota)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = quota.RetryAfterUtc
            });
        }

        _logger.LogError(ex, "Lỗi khi AI xử lý văn bản.");

        return StatusCode(StatusCodes.Status500InternalServerError, new
        {
            message = "Đã xảy ra lỗi khi nhờ AI xử lý văn bản."
        });
    }

    private sealed record DocumentBrief(string Title, string? Content);
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

public sealed class AskTextRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
}

public sealed class AskDocumentRequest
{
    public string Question { get; set; } = string.Empty;
}

public sealed class AskFileRequest
{
    public int RootIndex { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
}