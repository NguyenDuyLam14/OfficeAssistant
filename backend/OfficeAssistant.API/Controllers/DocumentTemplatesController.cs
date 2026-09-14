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
public class DocumentTemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DocumentTemplatesController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET: api/DocumentTemplates
    // Lấy danh sách template
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _context.DocumentTemplates
            .AsNoTracking()
            .OrderByDescending(template => template.CreatedAt)
            .Select(template => new
            {
                template.DocumentTemplateId,
                template.TemplateName,
                template.DocumentType,
                template.FilePath,
                template.Description,
                template.IsActive,
                template.CreatedAt
            })
            .ToListAsync();

        return Ok(templates);
    }

    // =========================================================
    // GET: api/DocumentTemplates/{id}
    // Lấy chi tiết một template
    // =========================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var template = await _context.DocumentTemplates
            .AsNoTracking()
            .Where(template =>
                template.DocumentTemplateId == id)
            .Select(template => new
            {
                template.DocumentTemplateId,
                template.TemplateName,
                template.DocumentType,
                template.FilePath,
                template.Description,
                template.IsActive,
                template.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy template văn bản."
            });
        }

        return Ok(template);
    }

    // =========================================================
    // POST: api/DocumentTemplates
    // Tạo template mới
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> CreateTemplate(
        CreateDocumentTemplateDto dto)
    {
        // Kiểm tra tên template.
        if (string.IsNullOrWhiteSpace(dto.TemplateName))
        {
            return BadRequest(new
            {
                message = "Tên template không được để trống."
            });
        }

        // Kiểm tra loại văn bản.
        if (string.IsNullOrWhiteSpace(dto.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        // Kiểm tra template trùng tên.
        var duplicate = await _context.DocumentTemplates
            .AnyAsync(template =>
                template.TemplateName == dto.TemplateName.Trim());

        if (duplicate)
        {
            return Conflict(new
            {
                message = "Template có tên này đã tồn tại."
            });
        }

        var template = new DocumentTemplate
        {
            TemplateName = dto.TemplateName.Trim(),

            DocumentType = dto.DocumentType.Trim(),

            FilePath = string.IsNullOrWhiteSpace(dto.FilePath)
                ? null
                : dto.FilePath.Trim(),

            Description = string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim(),

            IsActive = true,

            CreatedAt = DateTime.UtcNow
        };

        _context.DocumentTemplates.Add(template);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTemplate),
            new
            {
                id = template.DocumentTemplateId
            },
            new
            {
                template.DocumentTemplateId,
                template.TemplateName,
                template.DocumentType,
                template.FilePath,
                template.Description,
                template.IsActive,
                template.CreatedAt
            }
        );
    }

    // =========================================================
    // PUT: api/DocumentTemplates/{id}
    // Cập nhật template
    // =========================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTemplate(
        int id,
        UpdateDocumentTemplateDto dto)
    {
        // Kiểm tra tên template.
        if (string.IsNullOrWhiteSpace(dto.TemplateName))
        {
            return BadRequest(new
            {
                message = "Tên template không được để trống."
            });
        }

        // Kiểm tra loại văn bản.
        if (string.IsNullOrWhiteSpace(dto.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        // Tìm template.
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(template =>
                template.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy template văn bản."
            });
        }

        // Kiểm tra trùng tên với template khác.
        var duplicate = await _context.DocumentTemplates
            .AnyAsync(other =>
                other.DocumentTemplateId != id &&
                other.TemplateName == dto.TemplateName.Trim());

        if (duplicate)
        {
            return Conflict(new
            {
                message = "Template có tên này đã tồn tại."
            });
        }

        // Cập nhật dữ liệu.
        template.TemplateName =
            dto.TemplateName.Trim();

        template.DocumentType =
            dto.DocumentType.Trim();

        template.FilePath =
            string.IsNullOrWhiteSpace(dto.FilePath)
                ? null
                : dto.FilePath.Trim();

        template.Description =
            string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim();

        template.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật template thành công.",

            template.DocumentTemplateId,
            template.TemplateName,
            template.DocumentType,
            template.FilePath,
            template.Description,
            template.IsActive,
            template.CreatedAt
        });
    }

    // =========================================================
    // DELETE: api/DocumentTemplates/{id}
    // Xóa template
    // =========================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(template =>
                template.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy template văn bản."
            });
        }

        // Kiểm tra template có đang được sử dụng
        // bởi văn bản hay không.
        var isUsed = await _context.Documents
            .AnyAsync(document =>
                document.DocumentTemplateId == id);

        if (isUsed)
        {
            return BadRequest(new
            {
                message =
                    "Không thể xóa template vì template đang được sử dụng."
            });
        }

        _context.DocumentTemplates.Remove(template);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa template thành công."
        });
    }
}