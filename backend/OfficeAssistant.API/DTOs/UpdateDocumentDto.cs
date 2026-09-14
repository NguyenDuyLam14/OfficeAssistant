namespace OfficeAssistant.API.DTOs;

public class UpdateDocumentDto
{
    // Tiêu đề văn bản.
    public string Title { get; set; } = string.Empty;

    // Loại văn bản.
    public string DocumentType { get; set; } = string.Empty;

    // Nội dung văn bản.
    public string? Content { get; set; }

    // Template được sử dụng.
    public int? DocumentTemplateId { get; set; }
}