using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentTemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DocumentTemplatesController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // =========================================================
    // GET: api/DocumentTemplates
    // Lấy danh sách tất cả mẫu văn bản
    // =========================================================
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentTemplate>>> GetTemplates()
    {
        var templates = await _context.DocumentTemplates
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(templates);
    }

    // =========================================================
    // GET: api/DocumentTemplates/{id}
    // Lấy thông tin một mẫu văn bản
    // =========================================================
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentTemplate>> GetTemplate(int id)
    {
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(x => x.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy mẫu văn bản."
            });
        }

        return Ok(template);
    }

    // =========================================================
    // POST: api/DocumentTemplates
    // Tạo một mẫu văn bản mới
    // =========================================================
    [HttpPost]
    public async Task<ActionResult<DocumentTemplate>> CreateTemplate(
        CreateDocumentTemplateDto request)
    {
        // Kiểm tra tên mẫu.
        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest(new
            {
                message = "Tên mẫu văn bản không được để trống."
            });
        }

        // Kiểm tra loại văn bản.
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        // Kiểm tra trùng tên mẫu.
        var exists = await _context.DocumentTemplates
            .AnyAsync(x =>
                x.TemplateName == request.TemplateName);

        if (exists)
        {
            return Conflict(new
            {
                message = "Tên mẫu văn bản đã tồn tại."
            });
        }

        var template = new DocumentTemplate
        {
            TemplateName = request.TemplateName.Trim(),
            DocumentType = request.DocumentType.Trim(),
            FilePath = request.FilePath,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.DocumentTemplates.Add(template);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTemplate),
            new { id = template.DocumentTemplateId },
            template
        );
    }

    // =========================================================
    // PUT: api/DocumentTemplates/{id}
    // Cập nhật thông tin mẫu văn bản
    // =========================================================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTemplate(
        int id,
        UpdateDocumentTemplateDto request)
    {
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(x => x.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy mẫu văn bản."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest(new
            {
                message = "Tên mẫu văn bản không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        // Kiểm tra xem có mẫu khác đã sử dụng tên này chưa.
        var duplicate = await _context.DocumentTemplates
            .AnyAsync(x =>
                x.DocumentTemplateId != id &&
                x.TemplateName == request.TemplateName);

        if (duplicate)
        {
            return Conflict(new
            {
                message = "Tên mẫu văn bản đã tồn tại."
            });
        }

        template.TemplateName = request.TemplateName.Trim();
        template.DocumentType = request.DocumentType.Trim();
        template.FilePath = request.FilePath;
        template.Description = request.Description;
        template.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(template);
    }

    // =========================================================
    // DELETE: api/DocumentTemplates/{id}
    // Xóa mẫu văn bản
    // =========================================================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(x => x.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy mẫu văn bản."
            });
        }

        // Kiểm tra mẫu có đang được sử dụng bởi văn bản hay không.
        var isUsed = await _context.Documents
            .AnyAsync(x => x.DocumentTemplateId == id);

        if (isUsed)
        {
            return BadRequest(new
            {
                message =
                    "Không thể xóa mẫu văn bản vì mẫu đang được sử dụng."
            });
        }

        _context.DocumentTemplates.Remove(template);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã xóa mẫu văn bản."
        });
    }

    // =========================================================
    // POST: api/DocumentTemplates/{id}/upload
    //
    // Upload file Word .docx cho một mẫu văn bản.
    //
    // Form-data:
    // file = file .docx
    // =========================================================
    [HttpPost("{id:int}/upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadTemplateFile(
        int id,
        IFormFile file)
    {
        // -----------------------------------------------------
        // 1. Kiểm tra mẫu văn bản
        // -----------------------------------------------------
        var template = await _context.DocumentTemplates
            .FirstOrDefaultAsync(x =>
                x.DocumentTemplateId == id);

        if (template == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy mẫu văn bản."
            });
        }

        // -----------------------------------------------------
        // 2. Kiểm tra file có tồn tại hay không
        // -----------------------------------------------------
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Vui lòng chọn file Word để tải lên."
            });
        }

        // -----------------------------------------------------
        // 3. Giới hạn dung lượng
        // -----------------------------------------------------
        const long maxFileSize = 10 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            return BadRequest(new
            {
                message = "File không được vượt quá 10 MB."
            });
        }

        // -----------------------------------------------------
        // 4. Chỉ cho phép file .docx
        // -----------------------------------------------------
        var extension =
            Path.GetExtension(file.FileName);

        if (!extension.Equals(
                ".docx",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Chỉ cho phép tải lên file Word có định dạng .docx."
            });
        }

        // -----------------------------------------------------
        // 5. Tạo thư mục Storage/Templates nếu chưa tồn tại
        // -----------------------------------------------------
        var storagePath = Path.Combine(
            _environment.ContentRootPath,
            "Storage",
            "Templates"
        );

        Directory.CreateDirectory(storagePath);

        // -----------------------------------------------------
        // 6. Tạo tên file an toàn
        //
        // Không sử dụng trực tiếp tên file người dùng gửi lên
        // để tránh vấn đề ký tự đặc biệt hoặc trùng tên.
        // -----------------------------------------------------
        var safeFileName =
            $"{Guid.NewGuid():N}.docx";

        var physicalFilePath =
            Path.Combine(storagePath, safeFileName);

        // -----------------------------------------------------
        // 7. Nếu mẫu đã có file cũ thì xóa file cũ
        // -----------------------------------------------------
        if (!string.IsNullOrWhiteSpace(template.FilePath))
        {
            var oldFilePath =
                Path.Combine(
                    _environment.ContentRootPath,
                    template.FilePath.TrimStart(
                        '/',
                        '\\'
                    )
                );

            if (System.IO.File.Exists(oldFilePath))
            {
                try
                {
                    System.IO.File.Delete(oldFilePath);
                }
                catch
                {
                    // Nếu không xóa được file cũ thì vẫn tiếp tục.
                    // File mới vẫn sẽ được lưu.
                }
            }
        }

        // -----------------------------------------------------
        // 8. Lưu file mới xuống ổ đĩa
        // -----------------------------------------------------
        await using (var stream =
            new FileStream(
                physicalFilePath,
                FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // -----------------------------------------------------
        // 9. Lưu đường dẫn tương đối vào database
        //
        // Không lưu đường dẫn tuyệt đối kiểu:
        // C:\Nguyen_Duy_Lam\...
        //
        // Mà lưu:
        // Storage/Templates/abc123.docx
        // -----------------------------------------------------
        var relativePath =
            Path.Combine(
                "Storage",
                "Templates",
                safeFileName
            ).Replace("\\", "/");

        template.FilePath = relativePath;

        await _context.SaveChangesAsync();

        // -----------------------------------------------------
        // 10. Trả kết quả về frontend
        // -----------------------------------------------------
        return Ok(new
        {
            message = "Upload mẫu Word thành công.",
            documentTemplateId =
                template.DocumentTemplateId,
            templateName =
                template.TemplateName,
            fileName =
                file.FileName,
            filePath =
                template.FilePath
        });
    }
}