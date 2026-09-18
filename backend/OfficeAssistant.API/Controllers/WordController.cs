using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.Word;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Controller xử lý việc sinh file Word từ mẫu văn bản.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WordController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly WordTemplateService _wordTemplateService;

    public WordController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        WordTemplateService wordTemplateService)
    {
        _context = context;
        _environment = environment;
        _wordTemplateService = wordTemplateService;
    }

    /// <summary>
    /// Sinh file Word mới từ mẫu văn bản đã upload.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateWord(
        GenerateWordRequest request)
    {
        // =====================================================
        // 1. KIỂM TRA ID MẪU
        // =====================================================

        if (request.DocumentTemplateId <= 0)
        {
            return BadRequest(new
            {
                message = "DocumentTemplateId không hợp lệ."
            });
        }

        // =====================================================
        // 2. TÌM MẪU VĂN BẢN TRONG DATABASE
        // =====================================================

        var template =
            await _context.DocumentTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.DocumentTemplateId ==
                        request.DocumentTemplateId &&
                        x.IsActive
                );

        if (template == null)
        {
            return NotFound(new
            {
                message =
                    "Không tìm thấy mẫu văn bản."
            });
        }

        // =====================================================
        // 3. KIỂM TRA FILE WORD ĐÃ UPLOAD
        // =====================================================

        if (string.IsNullOrWhiteSpace(template.FilePath))
        {
            return BadRequest(new
            {
                message =
                    "Mẫu văn bản chưa có file Word được upload."
            });
        }

        // =====================================================
        // 4. XÁC ĐỊNH ĐƯỜNG DẪN FILE MẪU
        // =====================================================

        /*
         * Database đang lưu dạng:
         *
         * Storage/Templates/abc.docx
         *
         * Đây là đường dẫn tương đối tính từ thư mục
         * Backend.
         */

        var templateFilePath =
            Path.Combine(
                _environment.ContentRootPath,
                template.FilePath
                    .Replace(
                        "/",
                        Path.DirectorySeparatorChar.ToString()
                    )
            );

        // =====================================================
        // 5. KIỂM TRA FILE THỰC TẾ
        // =====================================================

        if (!System.IO.File.Exists(templateFilePath))
        {
            return NotFound(new
            {
                message =
                    "File Word của mẫu không tồn tại trên máy chủ.",
                filePath =
                    template.FilePath
            });
        }

        // =====================================================
        // 6. TẠO THƯ MỤC LƯU FILE WORD SINH RA
        // =====================================================

        var generatedDirectory =
            Path.Combine(
                _environment.ContentRootPath,
                "Storage",
                "Generated"
            );

        Directory.CreateDirectory(
            generatedDirectory
        );

        // =====================================================
        // 7. TẠO TÊN FILE MỚI
        // =====================================================

        /*
         * Dùng GUID để tránh trùng tên file.
         */

        var generatedFileName =
            $"Generated_{Guid.NewGuid():N}.docx";

        var generatedFilePath =
            Path.Combine(
                generatedDirectory,
                generatedFileName
            );

        // =====================================================
        // 8. THAY PLACEHOLDER VÀO FILE WORD
        // =====================================================

        try
        {
            _wordTemplateService.GenerateDocument(
                templateFilePath,
                generatedFilePath,
                request.Replacements
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "===== WORD GENERATION ERROR ====="
            );

            Console.WriteLine(
                ex.ToString()
            );

            Console.WriteLine(
                "=================================="
            );

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Không thể tạo file Word.",
                    detail =
                        ex.Message
                }
            );
        }

        // =====================================================
        // 9. KIỂM TRA FILE ĐÃ ĐƯỢC TẠO
        // =====================================================

        if (!System.IO.File.Exists(generatedFilePath))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Hệ thống không tạo được file Word."
                }
            );
        }

        // =====================================================
        // 10. ĐỌC FILE VÀ TRẢ VỀ CHO CLIENT
        // =====================================================

        var fileBytes =
            await System.IO.File.ReadAllBytesAsync(
                generatedFilePath
            );

        /*
         * File sẽ được trình duyệt tải xuống
         * với tên Generated_xxx.docx
         */

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            generatedFileName
        );
    }
}

/// <summary>
/// Dữ liệu gửi lên API để sinh file Word.
/// </summary>
public class GenerateWordRequest
{
    /// <summary>
    /// ID của mẫu văn bản trong bảng DocumentTemplates.
    /// </summary>
    public int DocumentTemplateId { get; set; }

    /// <summary>
    /// Danh sách placeholder cần thay thế.
    ///
    /// Ví dụ:
    /// "{{TIEU_DE}}" -> "THÔNG BÁO..."
    /// "{{NOI_DUNG}}" -> "Nội dung..."
    /// </summary>
    public Dictionary<string, string> Replacements { get; set; } =
        new();
}