
namespace OfficeAssistant.API.DTOs;

public class CreateDocumentTemplateDto
{
    // Tên của mẫu văn bản.
    public string TemplateName { get; set; } = string.Empty;

    // Loại văn bản: Thông báo, Công văn, Báo cáo...
    public string DocumentType { get; set; } = string.Empty;

    // Mô tả mẫu văn bản.
    public string? Description { get; set; }
}
