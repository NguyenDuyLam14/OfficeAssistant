using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Files;

namespace OfficeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PresentationController : ControllerBase
{
    private const int MinChars = 50;
    private const int MaxChars = 12000;

    private readonly IAIService _ai;
    private readonly ApplicationDbContext _db;
    private readonly LocalFileSearchService _files;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PresentationController> _logger;

    public PresentationController(
        IAIService ai, ApplicationDbContext db, LocalFileSearchService files,
        IWebHostEnvironment env, ILogger<PresentationController> logger)
    {
        _ai = ai; _db = db; _files = files; _env = env; _logger = logger;
    }

    // POST api/Presentation/outline
    // Nguồn: documentId (văn bản của chính user) HOẶC content (dữ liệu tự cung cấp).
    [HttpPost("outline")]
    public async Task<IActionResult> Outline(
        OutlineRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var slideCount = Math.Clamp(request.SlideCount, 3, 12);
        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;

        if (request.DocumentId.HasValue)
        {
            var doc = await _db.Documents.AsNoTracking()
                .Where(x => x.DocumentId == request.DocumentId && x.UserId == userId)
                .Select(x => new { x.Title, x.Content, x.FilePath })
                .FirstOrDefaultAsync(cancellationToken);

            if (doc == null)
                return NotFound(new { message = "Không tìm thấy văn bản." });

            title = doc.Title;
            content = doc.Content?.Trim() ?? string.Empty;

            // Văn bản xuất từ mẫu Word chưa có Content: đọc từ file .docx.
            if (content.Length == 0 && !string.IsNullOrWhiteSpace(doc.FilePath))
                content = ReadGeneratedDocx(doc.FilePath);
        }

        if (content.Length < MinChars)
            return BadRequest(new { message = "Nội dung quá ngắn để tạo bài thuyết trình." });

        if (content.Length > MaxChars) content = content[..MaxChars];

        try
        {
            var outline = await _ai.GenerateSlideOutlineAsync(
                title, content, slideCount, request.Audience, cancellationToken);

            return Ok(outline);
        }
        catch (GeminiQuotaException ex)
        {
            return StatusCode(429, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = ex.RetryAfterUtc
            });
        }
        catch (Exception ex) when (
            !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(ex, "Lỗi khi AI lập dàn ý slide.");
            return StatusCode(500, new { message = "Không thể tạo dàn ý bài thuyết trình." });
        }
    }

    // Chỉ đọc file nằm trong Storage/Generated (chống ../).
    private string ReadGeneratedDocx(string relativePath)
    {
        var baseDir = Path.GetFullPath(Path.Combine(
            _env.ContentRootPath, "Storage", "Generated")) + Path.DirectorySeparatorChar;

        var full = Path.GetFullPath(Path.Combine(
            _env.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
            return string.Empty;

        try { return _files.ExtractText(full); }
        catch { return string.Empty; }
    }
}

public sealed class OutlineRequest
{
    public int? DocumentId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int SlideCount { get; set; } = 6;
    public string? Audience { get; set; }
}