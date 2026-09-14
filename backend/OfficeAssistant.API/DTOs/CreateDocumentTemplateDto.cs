namespace OfficeAssistant.API.DTOs;

public class CreateDocumentTemplateDto
{
    // Tên của template.
    public string TemplateName { get; set; } = string.Empty;

    // Loại văn bản.
    // Ví dụ: Thông báo, Công văn, Báo cáo...
    public string DocumentType { get; set; } = string.Empty;

    // Đường dẫn đến file Word template.
    // Hiện tại có thể để null vì chúng ta chưa upload file Word.
    public string? FilePath { get; set; }

    // Mô tả template.
    public string? Description { get; set; }
}