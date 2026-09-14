namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Interface định nghĩa các chức năng AI
/// mà hệ thống OfficeAssistant sẽ sử dụng.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Sinh nội dung văn bản dựa trên:
    /// - Loại văn bản
    /// - Template được chọn
    /// - Yêu cầu của người dùng
    /// </summary>
    Task<string> GenerateDocumentAsync(
        string documentType,
        string templateName,
        string userPrompt);
}