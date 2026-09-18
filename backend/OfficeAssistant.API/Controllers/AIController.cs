using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.AI;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Controller xử lý các chức năng AI của hệ thống.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;

    public AIController(
        IAIService aiService,
        ApplicationDbContext context)
    {
        _aiService = aiService;
        _context = context;
    }

    /// <summary>
    /// Sinh nội dung văn bản bằng AI.
    ///
    /// Luồng xử lý:
    ///
    /// React
    ///   ↓
    /// documentTemplateId
    ///   ↓
    /// SQL Server
    ///   ↓
    /// lấy thông tin DocumentTemplate
    ///   ↓
    /// GeminiService
    ///   ↓
    /// Gemini API
    ///   ↓
    /// nội dung văn bản
    /// </summary>
    [HttpPost("generate-document")]
    public async Task<IActionResult> GenerateDocument(
        GenerateDocumentRequest request)
    {
        // =====================================================
        // 1. KIỂM TRA LOẠI VĂN BẢN
        // =====================================================

        if (string.IsNullOrWhiteSpace(
            request.DocumentType))
        {
            return BadRequest(new
            {
                message =
                    "Loại văn bản không được để trống."
            });
        }

        // =====================================================
        // 2. KIỂM TRA TEMPLATE ID
        // =====================================================

        if (request.DocumentTemplateId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Vui lòng chọn mẫu văn bản hợp lệ."
            });
        }

        // =====================================================
        // 3. KIỂM TRA YÊU CẦU SOẠN THẢO
        // =====================================================

        if (string.IsNullOrWhiteSpace(
            request.UserPrompt))
        {
            return BadRequest(new
            {
                message =
                    "Yêu cầu soạn thảo không được để trống."
            });
        }

        // =====================================================
        // 4. TÌM TEMPLATE TRONG DATABASE
        // =====================================================

        var template =
            await _context.DocumentTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    template =>
                        template.DocumentTemplateId ==
                        request.DocumentTemplateId &&
                        template.IsActive);

        // Không tìm thấy template.
        if (template == null)
        {
            return NotFound(new
            {
                message =
                    "Không tìm thấy mẫu văn bản hoặc mẫu đã bị vô hiệu hóa."
            });
        }

        // =====================================================
        // 5. GỌI AI
        // =====================================================

        try
        {
            var content =
                await _aiService.GenerateDocumentAsync(
                    request.DocumentType,
                    template.TemplateName,
                    request.UserPrompt
                );

            // =================================================
            // 6. TRẢ KẾT QUẢ VỀ FRONTEND
            // =================================================

            return Ok(new
            {
                message =
                    "AI đã tạo nội dung văn bản thành công.",

                content,

                // ID của template được sử dụng.
                documentTemplateId =
                    template.DocumentTemplateId,

                // Tên template.
                templateName =
                    template.TemplateName,

                // Loại văn bản của template.
                documentType =
                    template.DocumentType
            });
        }
        catch (Exception ex)
        {
            // Ghi lỗi vào console để phục vụ debug.
            Console.WriteLine(
                "===== AI ERROR ====="
            );

            Console.WriteLine(
                ex.ToString()
            );

            Console.WriteLine(
                "===================="
            );

            // Không trả chi tiết exception về frontend.
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Đã xảy ra lỗi khi xử lý yêu cầu AI."
                }
            );
        }
    }
}

/// <summary>
/// Dữ liệu frontend gửi lên khi yêu cầu AI
/// soạn thảo văn bản.
/// </summary>
public class GenerateDocumentRequest
{
    // Loại văn bản người dùng muốn tạo.
    public string DocumentType { get; set; } = string.Empty;

    // ID mẫu văn bản lấy từ bảng DocumentTemplates.
    public int DocumentTemplateId { get; set; }

    // Yêu cầu cụ thể của người dùng.
    public string UserPrompt { get; set; } = string.Empty;
}