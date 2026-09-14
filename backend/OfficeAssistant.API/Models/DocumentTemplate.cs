namespace OfficeAssistant.API.Models;

/// <summary>
/// Lưu thông tin mẫu văn bản được sử dụng
/// để tạo văn bản bằng AI.
/// </summary>
public class DocumentTemplate
{
    /// <summary>
    /// Khóa chính của mẫu văn bản.
    /// </summary>
    public int DocumentTemplateId { get; set; }

    /// <summary>
    /// Tên mẫu văn bản.
    /// Ví dụ: Thông báo, Công văn, Báo cáo.
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Loại văn bản.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn tới file Word mẫu.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Mô tả mẫu văn bản.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Cho biết mẫu có đang được sử dụng hay không.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Thời điểm tạo mẫu.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}