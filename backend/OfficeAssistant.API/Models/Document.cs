namespace OfficeAssistant.API.Models;

/// <summary>
/// Đại diện cho văn bản được tạo và quản lý trong hệ thống.
/// </summary>
public class Document
{
    /// <summary>
    /// Khóa chính của văn bản.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Tiêu đề văn bản.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Loại văn bản.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung văn bản.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Đường dẫn tới file Word sau khi xuất.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// ID của người tạo văn bản.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// ID của mẫu văn bản được sử dụng.
    /// Có thể null nếu văn bản không sử dụng mẫu.
    /// </summary>
    public int? DocumentTemplateId { get; set; }

    /// <summary>
    /// Thời điểm tạo văn bản.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm cập nhật gần nhất.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Quan hệ tới người dùng.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Quan hệ tới mẫu văn bản.
    /// </summary>
    public DocumentTemplate? DocumentTemplate { get; set; }
}
