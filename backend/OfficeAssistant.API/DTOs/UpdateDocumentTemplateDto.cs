
namespace OfficeAssistant.API.DTOs;

public class UpdateDocumentTemplateDto
{
    // Tên mẫu văn bản.
    public string TemplateName { get; set; } = string.Empty;

    // Loại văn bản.
    public string DocumentType { get; set; } = string.Empty;

    // Mô tả mẫu văn bản.
    public string? Description { get; set; }

    // Cho phép bật hoặc tắt mẫu văn bản.
    public bool IsActive { get; set; } = true;
}
