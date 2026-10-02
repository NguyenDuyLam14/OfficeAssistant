namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Interface xử lý các chức năng AI.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Tạo nội dung văn bản thông thường.
    /// </summary>
    Task<string> GenerateDocumentAsync(
        string documentType,
        string templateName,
        string userPrompt
    );

    /// <summary>
    /// Tạo dữ liệu cho các placeholder của Word template.
    ///
    /// Kết quả trả về Dictionary:
    /// Key   = placeholder
    /// Value = nội dung Gemini sinh ra
    /// </summary>
    Task<Dictionary<string, string>> GenerateDocumentFieldsAsync(
        string documentType,
        string templateName,
        string userPrompt,
        List<string> placeholders
    );
}