using System.Net;
using System.Text;
using System.Text.Json;

namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Service giao tiếp với Gemini API.
///
/// Chức năng:
/// - Gửi yêu cầu soạn thảo văn bản tới Gemini.
/// - Nhận nội dung văn bản do AI sinh ra.
/// - Tự động retry khi Gemini tạm thời quá tải
///   hoặc gặp lỗi 429/5xx.
/// </summary>
public class GeminiService : IAIService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public GeminiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Sinh nội dung văn bản bằng Gemini.
    /// </summary>
    public async Task<string> GenerateDocumentAsync(
        string documentType,
        string templateName,
        string userPrompt)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình Gemini API Key."
            );
        }

        // Lấy model từ User Secrets.
        var modelName =
            _configuration["Gemini:Model"]
            ?? "gemini-3.6-flash";

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

        var systemPrompt = """
            Bạn là trợ lý AI hỗ trợ nghiệp vụ văn phòng.

            Nhiệm vụ:
            - Soạn thảo văn bản hành chính rõ ràng, chính xác.
            - Sử dụng văn phong hành chính phù hợp.
            - Nội dung phải phù hợp với loại văn bản được yêu cầu.
            - Không tự ý bịa đặt thông tin mà người dùng chưa cung cấp.
            - Nếu thiếu thông tin quan trọng, để phần thông tin cần
              bổ sung thay vì tự tạo dữ liệu.
            - Nội dung phải có cấu trúc rõ ràng.
            - Không giải thích quá trình suy luận.
            - Chỉ trả về nội dung văn bản cần soạn thảo.
            """;

        var userMessage = $"""
            {systemPrompt}

            Loại văn bản:
            {documentType}

            Mẫu văn bản:
            {templateName}

            Yêu cầu của người dùng:
            {userPrompt}

            Hãy soạn thảo nội dung văn bản phù hợp.
            """;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = userMessage
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        // Thử tối đa 3 lần.
        const int maxRetries = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                url
            );

            // Gemini xác thực bằng x-goog-api-key.
            request.Headers.Add(
                "x-goog-api-key",
                apiKey
            );

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            using var response =
                await _httpClient.SendAsync(request);

            var responseContent =
                await response.Content.ReadAsStringAsync();

            // Nếu request thành công.
            if (response.IsSuccessStatusCode)
            {
                return ExtractGeneratedText(responseContent);
            }

            // Gemini 503:
            // Model/server đang tạm thời quá tải.
            //
            // Gemini cũng có thể trả 429 hoặc 5xx
            // cho các lỗi tạm thời.
            var shouldRetry =
                response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                (int)response.StatusCode >= 500;

            if (!shouldRetry)
            {
                throw new InvalidOperationException(
                    $"Gemini API trả về HTTP {(int)response.StatusCode}: " +
                    responseContent
                );
            }

            // Nếu vẫn còn lần retry:
            // 1 giây → 2 giây → 4 giây.
            if (attempt < maxRetries)
            {
                var delaySeconds =
                    Math.Pow(2, attempt - 1);

                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds)
                );
            }
            else
            {
                throw new InvalidOperationException(
                    $"Gemini API tạm thời không khả dụng " +
                    $"sau {maxRetries} lần thử. " +
                    $"HTTP {(int)response.StatusCode}: " +
                    responseContent
                );
            }
        }

        throw new InvalidOperationException(
            "Không thể nhận phản hồi từ Gemini API."
        );
    }

    /// <summary>
    /// Lấy nội dung text từ response JSON của Gemini.
    /// </summary>
    private static string ExtractGeneratedText(
        string responseContent)
    {
        using var jsonDocument =
            JsonDocument.Parse(responseContent);

        var root =
            jsonDocument.RootElement;

        if (!root.TryGetProperty(
                "candidates",
                out var candidates) ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini không trả về candidate."
            );
        }

        var candidate =
            candidates[0];

        if (!candidate.TryGetProperty(
                "content",
                out var content))
        {
            throw new InvalidOperationException(
                "Gemini không trả về content."
            );
        }

        if (!content.TryGetProperty(
                "parts",
                out var parts) ||
            parts.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini không trả về phần nội dung."
            );
        }

        var text =
            parts[0]
                .GetProperty("text")
                .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini trả về nội dung rỗng."
            );
        }

        return text;
    }
}