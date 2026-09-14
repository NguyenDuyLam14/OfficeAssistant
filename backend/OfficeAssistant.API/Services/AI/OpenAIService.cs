using OpenAI.Chat;

namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Service chịu trách nhiệm giao tiếp với OpenAI API.
/// 
/// Lớp này được tách riêng khỏi Controller để:
/// - Controller không chứa logic AI.
/// - Có thể thay đổi model/API sau này.
/// - Dễ kiểm thử và bảo trì.
/// </summary>
public class OpenAIService : IAIService
{
    private readonly IConfiguration _configuration;

    public OpenAIService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Sinh nội dung văn bản bằng mô hình ngôn ngữ.
    /// </summary>
    public async Task<string> GenerateDocumentAsync(
        string documentType,
        string templateName,
        string userPrompt)
    {
        /*
         * Lấy API Key từ cấu hình.
         *
         * Hiện tại chúng ta chưa cấu hình API Key.
         * Vì vậy nếu chưa có key, hệ thống sẽ báo lỗi
         * rõ ràng thay vì ứng dụng bị lỗi không xác định.
         */
        var apiKey = _configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình OpenAI API Key."
            );
        }

        /*
         * Lấy tên model từ cấu hình.
         *
         * Việc đặt model trong cấu hình giúp chúng ta
         * có thể thay đổi model mà không phải sửa code.
         */
        var modelName =
            _configuration["OpenAI:Model"]
            ?? "gpt-4o-mini";

        /*
         * Tạo ChatClient của OpenAI.
         */
        ChatClient client = new ChatClient(
            model: modelName,
            apiKey: apiKey
        );

        /*
         * System Prompt:
         *
         * Xác định vai trò của AI.
         *
         * Đây chính là một phần của Prompt Engineering
         * trong đề tài.
         */
        var systemPrompt = """
            Bạn là trợ lý AI hỗ trợ nghiệp vụ văn phòng.

            Nhiệm vụ:
            - Soạn thảo văn bản hành chính rõ ràng, chính xác.
            - Sử dụng văn phong hành chính phù hợp.
            - Không tự ý bịa đặt thông tin mà người dùng chưa cung cấp.
            - Nếu thiếu thông tin quan trọng, sử dụng cách diễn đạt
              phù hợp hoặc để phần thông tin cần bổ sung.
            - Nội dung phải có cấu trúc rõ ràng.
            - Không giải thích quá trình suy luận.
            - Chỉ trả về nội dung văn bản cần soạn thảo.
            """;

        /*
         * User Prompt:
         *
         * Ghép thông tin Template + loại văn bản +
         * yêu cầu cụ thể của người dùng.
         */
        var userMessage = $"""
            Loại văn bản: {documentType}

            Mẫu văn bản được chọn: {templateName}

            Yêu cầu của người dùng:
            {userPrompt}

            Hãy soạn thảo nội dung văn bản phù hợp
            với yêu cầu trên.
            """;

        /*
         * Gửi yêu cầu tới OpenAI.
         */
        ChatCompletion completion =
            await client.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                ]
            );

        /*
         * Lấy nội dung AI trả về.
         */
        var result = completion.Content.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                "AI không trả về nội dung văn bản."
            );
        }

        return result;
    }
}