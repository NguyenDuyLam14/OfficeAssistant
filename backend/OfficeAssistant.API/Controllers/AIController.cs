using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AIController(IAIService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Sinh nội dung văn bản bằng AI.
    /// </summary>
    [HttpPost("generate-document")]
    public async Task<IActionResult> GenerateDocument(
        GenerateDocumentRequest request)
    {
        /*
         * Kiểm tra dữ liệu đầu vào.
         */
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            return BadRequest(new
            {
                message = "Loại văn bản không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest(new
            {
                message = "Tên mẫu văn bản không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return BadRequest(new
            {
                message = "Yêu cầu soạn thảo không được để trống."
            });
        }

        try
        {
            /*
             * Gọi AI Service để sinh nội dung.
             */
            var content =
                await _aiService.GenerateDocumentAsync(
                    request.DocumentType,
                    request.TemplateName,
                    request.UserPrompt
                );

            /*
             * Trả kết quả về frontend.
             */
            return Ok(new
            {
                message = "AI đã tạo nội dung văn bản thành công.",
                content
            });
        }
        catch (InvalidOperationException ex)
        {
            /*
             * Các lỗi cấu hình hoặc lỗi nghiệp vụ
             * được trả về dưới dạng BadRequest.
             */
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception)
        {
            /*
             * Không trả chi tiết Exception ra frontend
             * để tránh làm lộ thông tin hệ thống.
             */
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
    /// <summary>
    /// Loại văn bản cần tạo.
    /// Ví dụ: Thông báo, Công văn, Báo cáo...
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Tên mẫu văn bản được chọn.
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Yêu cầu cụ thể của người dùng.
    /// </summary>
    public string UserPrompt { get; set; } = string.Empty;
}