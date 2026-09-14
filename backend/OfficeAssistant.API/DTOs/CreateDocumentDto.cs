namespace OfficeAssistant.API.DTOs;

public class CreateDocumentDto
{
    // Tiêu đề văn bản.
    public string Title { get; set; } = string.Empty;

    // Loại văn bản.
    // Ví dụ: Thông báo, Công văn, Báo cáo, Biên bản...
    public string DocumentType { get; set; } = string.Empty;

    // Nội dung văn bản.
    public string? Content { get; set; }

    // Template được sử dụng để tạo văn bản.
    public int? DocumentTemplateId { get; set; }
}