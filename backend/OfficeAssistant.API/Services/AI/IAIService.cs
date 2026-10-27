namespace OfficeAssistant.API.Services.AI;

using OfficeAssistant.API.DTOs.Email;

/// <summary>
/// Interface xử lý các chức năng AI.
/// </summary>
public interface IAIService
{
    Task<WorkEventExtractionResult?> AnalyzeWorkEventAsync(
        string emailSubject, string emailSender,
        string emailDate, string emailBody,
        CancellationToken cancellationToken = default);

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
    /// Value = nội dung AI sinh ra
    /// </summary>
    Task<Dictionary<string, string>> GenerateDocumentFieldsAsync(
        string documentType,
        string templateName,
        string userPrompt,
        List<string> placeholders
    );

    Task<Dictionary<string, string>> GenerateNaturalTemplateFieldsAsync(
    string documentType,
    string templateName,
    string userPrompt,
    string analyzedTemplateText,
    List<string> fieldIds
);
}