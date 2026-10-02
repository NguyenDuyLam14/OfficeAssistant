using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.Word;
using OfficeAssistant.API.Services.AI;

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
    private readonly IAIService _aiService;

    public WordController(
    ApplicationDbContext context,
    IWebHostEnvironment environment,
    WordTemplateService wordTemplateService,
    IAIService aiService)
    {
        _context = context;
        _environment = environment;
        _wordTemplateService = wordTemplateService;
        _aiService = aiService;
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

    /// <summary>
    /// AI tự động soạn nội dung theo yêu cầu người dùng,
    /// sau đó áp dụng nội dung vào Word template
    /// và trả về file .docx hoàn chỉnh.
    /// </summary>
    [HttpPost("generate-from-ai")]
    public async Task<IActionResult> GenerateWordFromAI(
        GenerateWordFromAIRequest request)
    {
        // =====================================================
        // 1. KIỂM TRA TEMPLATE ID
        // =====================================================

        if (request.DocumentTemplateId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "DocumentTemplateId không hợp lệ."
            });
        }

        // =====================================================
        // 2. KIỂM TRA USER PROMPT
        // =====================================================

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return BadRequest(new
            {
                message =
                    "Yêu cầu soạn thảo không được để trống."
            });
        }

        // =====================================================
        // 3. TÌM TEMPLATE
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
        // 4. KIỂM TRA FILE TEMPLATE
        // =====================================================

        if (string.IsNullOrWhiteSpace(
                template.FilePath))
        {
            return BadRequest(new
            {
                message =
                    "Mẫu văn bản chưa có file Word được upload."
            });
        }

        // =====================================================
        // 5. TẠO ĐƯỜNG DẪN TEMPLATE
        // =====================================================

        var templateFilePath =
            Path.Combine(
                _environment.ContentRootPath,
                template.FilePath.Replace(
                    "/",
                    Path.DirectorySeparatorChar.ToString()
                )
            );

        if (!System.IO.File.Exists(
                templateFilePath))
        {
            return NotFound(new
            {
                message =
                    "File Word của mẫu không tồn tại."
            });
        }

        try
        {
            // =================================================
            // 6. ĐỌC PLACEHOLDER TỪ TEMPLATE
            // =================================================

            var placeholders =
                _wordTemplateService.GetPlaceholders(
                    templateFilePath
                );

            if (placeholders.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Template không chứa placeholder dạng {{...}}."
                });
            }

            // =================================================
            // 7. GỌI GEMINI
            // =================================================

            var replacements =
                await _aiService.GenerateDocumentFieldsAsync(
                    template.DocumentType,
                    template.TemplateName,
                    request.UserPrompt,
                    placeholders
                );

            // =================================================
            // 8. TẠO THƯ MỤC GENERATED
            // =================================================

            var generatedDirectory =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Storage",
                    "Generated"
                );

            Directory.CreateDirectory(
                generatedDirectory
            );

            // =================================================
            // 9. ĐẶT TÊN FILE
            // =================================================

            var generatedFileName =
                $"AI_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.docx";

            var generatedFilePath =
                Path.Combine(
                    generatedDirectory,
                    generatedFileName
                );

            // =================================================
            // 10. ÁP DỤNG NỘI DUNG AI VÀO TEMPLATE
            // =================================================

            _wordTemplateService.GenerateDocument(
                templateFilePath,
                generatedFilePath,
                replacements
            );

            // =================================================
            // 11. KIỂM TRA FILE
            // =================================================

            if (!System.IO.File.Exists(
                    generatedFilePath))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "Không tạo được file Word."
                    }
                );
            }

            // =================================================
            // 12. ĐỌC FILE
            // =================================================

            var fileBytes =
                await System.IO.File.ReadAllBytesAsync(
                    generatedFilePath
                );

            // =================================================
            // 13. TRẢ FILE CHO FRONTEND
            // =================================================

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                generatedFileName
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "===== AI WORD GENERATION ERROR ====="
            );

            Console.WriteLine(
                ex.ToString()
            );

            Console.WriteLine(
                "====================================="
            );

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Không thể tạo file Word bằng AI.",
                    detail =
                        ex.Message
                }
            );
        }
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

/// <summary>
/// Dữ liệu gửi lên API để AI tự động
/// soạn nội dung và sinh file Word.
/// </summary>
public class GenerateWordFromAIRequest
{
    /// <summary>
    /// ID của mẫu Word.
    /// </summary>
    public int DocumentTemplateId { get; set; }

    /// <summary>
    /// Loại văn bản.
    /// </summary>
    public string DocumentType { get; set; } =
        string.Empty;

    /// <summary>
    /// Yêu cầu soạn thảo của người dùng.
    /// </summary>
    public string UserPrompt { get; set; } =
        string.Empty;
}
