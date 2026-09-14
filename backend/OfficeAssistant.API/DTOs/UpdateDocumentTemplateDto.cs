namespace OfficeAssistant.API.DTOs;

public class UpdateDocumentTemplateDto
{
    // Tên template.
    public string TemplateName { get; set; } = string.Empty;

    // Loại văn bản.
    public string DocumentType { get; set; } = string.Empty;

    // Đường dẫn file Word template.
    public string? FilePath { get; set; }

    // Mô tả template.
    public string? Description { get; set; }

    // Cho phép bật/tắt template.
    public bool IsActive { get; set; } = true;
}