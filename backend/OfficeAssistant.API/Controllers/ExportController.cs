using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.Word;

namespace OfficeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ExportController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ExportController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // POST api/Export/summary-docx
    [HttpPost("summary-docx")]
    public async Task<IActionResult> SummaryDocx(
        ExportSummaryRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;

        if (title.Length == 0 || title.Length > 250)
            return BadRequest(new { message = "Tiêu đề không được để trống (tối đa 250 ký tự)." });

        if (content.Length < 20 || content.Length > 50000)
            return BadRequest(new { message = "Nội dung tóm tắt không hợp lệ." });

        var dir = Path.Combine(_env.ContentRootPath, "Storage", "Generated");
        Directory.CreateDirectory(dir);

        var fileName = $"Summary_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.docx";
        var fullPath = Path.Combine(dir, fileName);

        try
        {
            SummaryDocxBuilder.Build(fullPath, title, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, new { message = "Không thể tạo file Word." });
        }

        var document = new Document
        {
            Title = title.Length > 300 ? title[..300] : title,
            DocumentType = "Báo cáo",
            Content = content,
            FilePath = $"Storage/Generated/{fileName}",
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        Response.Headers["X-Document-Id"] = document.DocumentId.ToString();

        return PhysicalFile(
            fullPath,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }
}

public sealed class ExportSummaryRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}