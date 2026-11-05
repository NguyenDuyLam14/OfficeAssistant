using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAssistant.API.Services.Files;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Tìm và tải file văn bản trong các thư mục được cấu hình.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class LocalFilesController : ControllerBase
{
    private readonly LocalFileSearchService _files;

    public LocalFilesController(LocalFileSearchService files)
    {
        _files = files;
    }

    // GET api/LocalFiles/roots
    [HttpGet("roots")]
    public IActionResult GetRoots() => Ok(_files.GetRoots());

    // GET api/LocalFiles/search?keyword=&category=&from=&to=&searchContent=
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool searchContent = false,
        [FromQuery] int? rootIndex = null,
        CancellationToken cancellationToken = default)
    {
        if (keyword is { Length: > 200 })
        {
            return BadRequest(new { message = "Từ khóa tối đa 200 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(category) &&
            !LocalFileSearchService.Categories.ContainsKey(category))
        {
            return BadRequest(new { message = "Loại file không hợp lệ." });
        }

        if (from.HasValue && to.HasValue && to <= from)
        {
            return BadRequest(new
            {
                message = "Khoảng ngày không hợp lệ (đến ngày phải sau từ ngày)."
            });
        }

        var result = await _files.SearchAsync(
            new LocalFileSearchQuery
            {
                Keyword = keyword,
                Category = category,
                FromUtc = from?.ToUniversalTime(),
                ToUtc = to?.ToUniversalTime(),
                SearchContent = searchContent,
                RootIndex = rootIndex
            },
            cancellationToken);

        return Ok(result);
    }

    // GET api/LocalFiles/download?rootIndex=0&path=abc/def.docx
    [HttpGet("download")]
    public IActionResult Download(
        [FromQuery] int rootIndex, [FromQuery] string? path)
    {
        var full = _files.ResolvePath(rootIndex, path);

        if (full == null)
        {
            return NotFound(new { message = "Không tìm thấy file." });
        }

        return PhysicalFile(
            full, "application/octet-stream", Path.GetFileName(full));
    }
}