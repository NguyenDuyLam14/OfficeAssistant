using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DocumentsController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET: api/documents
    // Lấy danh sách văn bản của người dùng hiện tại.
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetDocuments()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var documents = await _context.Documents
            .AsNoTracking()
            .Where(document => document.UserId == userId.Value)
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new
            {
                document.DocumentId,
                document.Title,
                document.DocumentType,
                document.Content,
                document.UserId,
                document.DocumentTemplateId,
                document.CreatedAt,
                document.UpdatedAt
            })
            .ToListAsync();

        return Ok(documents);
    }

    // =========================================================
    // GET: api/documents/5
    // Lấy thông tin một văn bản.
    // =========================================================
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var document = await _context.Documents
            .AsNoTracking()
            .Where(document =>
                document.DocumentId == id &&
                document.UserId == userId.Value)
            .Select(document => new
            {
                document.DocumentId,
                document.Title,
                document.DocumentType,
                document.Content,
                document.UserId,
                document.DocumentTemplateId,
                document.CreatedAt,
                document.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (document == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy văn bản."
            });
        }

        return Ok(document);
    }

    // =========================================================
    // POST: api/documents
    // Tạo văn bản mới.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> CreateDocument(
        CreateDocumentDto dto)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new
            {
                message = "Tiêu đề văn bản không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        // Nếu người dùng chọn template,
        // kiểm tra template có tồn tại hay không.
        if (dto.DocumentTemplateId.HasValue)
        {
            var templateExists =
                await _context.DocumentTemplates
                    .AnyAsync(template =>
                        template.DocumentTemplateId ==
                        dto.DocumentTemplateId.Value &&
                        template.IsActive);

            if (!templateExists)
            {
                return BadRequest(new
                {
                    message = "Template văn bản không tồn tại hoặc đã bị vô hiệu hóa."
                });
            }
        }

        var document = new Document
        {
            Title = dto.Title.Trim(),
            DocumentType = dto.DocumentType.Trim(),
            Content = dto.Content,
            UserId = userId.Value,
            DocumentTemplateId =
                dto.DocumentTemplateId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetDocument),
            new
            {
                id = document.DocumentId
            },
            new
            {
                document.DocumentId,
                document.Title,
                document.DocumentType,
                document.Content,
                document.UserId,
                document.DocumentTemplateId,
                document.CreatedAt,
                document.UpdatedAt
            }
        );
    }

    // =========================================================
    // PUT: api/documents/5
    // Cập nhật văn bản.
    // =========================================================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDocument(
        int id,
        UpdateDocumentDto dto)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new
            {
                message = "Tiêu đề văn bản không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(document =>
                document.DocumentId == id &&
                document.UserId == userId.Value);

        if (document == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy văn bản."
            });
        }

        if (dto.DocumentTemplateId.HasValue)
        {
            var templateExists =
                await _context.DocumentTemplates
                    .AnyAsync(template =>
                        template.DocumentTemplateId ==
                        dto.DocumentTemplateId.Value &&
                        template.IsActive);

            if (!templateExists)
            {
                return BadRequest(new
                {
                    message = "Template văn bản không tồn tại hoặc đã bị vô hiệu hóa."
                });
            }
        }

        document.Title = dto.Title.Trim();
        document.DocumentType = dto.DocumentType.Trim();
        document.Content = dto.Content;
        document.DocumentTemplateId =
            dto.DocumentTemplateId;

        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật văn bản thành công.",
            document.DocumentId,
            document.Title,
            document.DocumentType,
            document.Content,
            document.UserId,
            document.DocumentTemplateId,
            document.CreatedAt,
            document.UpdatedAt
        });
    }

    // =========================================================
    // DELETE: api/documents/5
    // Xóa văn bản.
    // =========================================================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDocument(
        int id)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(document =>
                document.DocumentId == id &&
                document.UserId == userId.Value);

        if (document == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy văn bản."
            });
        }

        _context.Documents.Remove(document);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa văn bản thành công."
        });
    }

    // =========================================================
    // Lấy UserId từ JWT.
    // =========================================================
    private int? GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (int.TryParse(
                userIdClaim,
                out var userId))
        {
            return userId;
        }

        return null;
    }
}