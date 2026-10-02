using System.Net;
using System.Text;
using System.Text.Json;

namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Service giao tiếp với Gemini API.
///
/// Chức năng:
/// - Gửi yêu cầu soạn thảo văn bản tới Gemini.
/// - Sinh nội dung văn bản thông thường.
/// - Sinh dữ liệu cho các placeholder của Word template.
/// - Sử dụng Structured Output khi sinh dữ liệu cho Word.
/// - Tự động retry khi Gemini gặp lỗi tạm thời.
/// </summary>
public class GeminiService : IAIService
{
    // =========================================================
    // CẤU HÌNH VÀ HTTP CLIENT
    // =========================================================

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Khởi tạo GeminiService.
    /// </summary>
    public GeminiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;

        // Lấy HttpClient thông qua IHttpClientFactory.
        _httpClient =
            httpClientFactory.CreateClient();
    }

    // =========================================================
    // 1. SINH NỘI DUNG VĂN BẢN THÔNG THƯỜNG
    // =========================================================

    /// <summary>
    /// Sinh nội dung văn bản bằng Gemini.
    ///
    /// Chức năng này dùng cho endpoint AI hiện tại:
    /// POST /api/AI/generate-document
    /// </summary>
    public async Task<string> GenerateDocumentAsync(
        string documentType,
        string templateName,
        string userPrompt)
    {
        // -----------------------------------------------------
        // Kiểm tra API Key.
        // -----------------------------------------------------

        var apiKey =
            GetApiKey();

        // -----------------------------------------------------
        // Lấy model từ User Secrets / Configuration.
        // -----------------------------------------------------

        var modelName =
            GetModelName();

        // -----------------------------------------------------
        // Prompt hệ thống.
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // Nội dung người dùng gửi cho AI.
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // Request body.
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // Gọi Gemini.
        // -----------------------------------------------------

        var responseContent =
            await SendGeminiRequestAsync(
                modelName,
                apiKey,
                requestBody
            );

        // -----------------------------------------------------
        // Đọc nội dung AI.
        // -----------------------------------------------------

        return ExtractGeneratedText(
            responseContent
        );
    }

    // =========================================================
    // 2. SINH DỮ LIỆU CHO WORD TEMPLATE
    // =========================================================

    /// <summary>
    /// Sử dụng Gemini để tạo dữ liệu cho các placeholder
    /// trong file Word template.
    ///
    /// Ví dụ template có:
    ///
    /// {{TIEU_DE}}
    /// {{DON_VI}}
    /// {{NOI_DUNG}}
    /// {{NGAY}}
    /// {{THANG}}
    /// {{NAM}}
    ///
    /// Gemini sẽ trả về:
    ///
    /// {
    ///   "{{TIEU_DE}}": "...",
    ///   "{{DON_VI}}": "...",
    ///   "{{NOI_DUNG}}": "...",
    ///   "{{NGAY}}": "05",
    ///   "{{THANG}}": "10",
    ///   "{{NAM}}": "2026"
    /// }
    /// </summary>
    public async Task<Dictionary<string, string>>
        GenerateDocumentFieldsAsync(
            string documentType,
            string templateName,
            string userPrompt,
            List<string> placeholders)
    {
        // -----------------------------------------------------
        // Kiểm tra placeholder.
        // -----------------------------------------------------

        if (placeholders == null ||
            placeholders.Count == 0)
        {
            throw new InvalidOperationException(
                "Template không chứa placeholder để AI điền nội dung."
            );
        }

        // -----------------------------------------------------
        // Lấy API Key.
        // -----------------------------------------------------

        var apiKey =
            GetApiKey();

        // -----------------------------------------------------
        // Lấy model.
        // -----------------------------------------------------

        var modelName =
            GetModelName();

        // -----------------------------------------------------
        // Tạo JSON Schema.
        // -----------------------------------------------------

        var properties =
            new Dictionary<string, object>();

        foreach (var placeholder in placeholders)
        {
            properties[placeholder] =
                new
                {
                    type = "string",

                    description =
                        $"Nội dung cần điền cho trường {placeholder}."
                };
        }

        var schema = new
        {
            type = "OBJECT",

            properties = properties,

            required = placeholders
        };

        // -----------------------------------------------------
        // Tạo danh sách placeholder cho prompt.
        // -----------------------------------------------------

        var placeholderText =
            string.Join(
                "\n",
                placeholders.Select(
                    placeholder =>
                        $"- {placeholder}"
                )
            );

        // -----------------------------------------------------
        // Prompt cho Gemini.
        // -----------------------------------------------------

        var prompt =
    "Bạn là trợ lý văn phòng thông minh hỗ trợ soạn thảo văn bản hành chính bằng tiếng Việt.\n\n" +
    "Loại văn bản:\n" +
    documentType +
    "\n\n" +
    "Tên mẫu văn bản:\n" +
    templateName +
    "\n\n" +
    "Yêu cầu của người dùng:\n" +
    userPrompt +
    "\n\n" +
    "Các placeholder có trong mẫu Word:\n" +
    placeholderText +
    "\n\n" +
    "Nhiệm vụ:\n" +
    "1. Phân tích yêu cầu của người dùng.\n" +
    "2. Soạn nội dung phù hợp với loại văn bản.\n" +
    "3. Điền nội dung tương ứng cho từng placeholder.\n" +
    "4. Phải giữ nguyên chính xác tên các placeholder.\n" +
    "5. Không thêm placeholder mới.\n" +
    "6. Không bỏ bất kỳ placeholder nào.\n" +
    "7. Chỉ trả về dữ liệu theo JSON Schema được cung cấp.\n" +
    "8. Không sử dụng Markdown.\n" +
    "9. Không thêm lời giải thích bên ngoài JSON.\n" +
    "10. Sử dụng văn phong hành chính tiếng Việt.\n" +
    "11. Không tự ý bịa đặt thông tin quan trọng.\n" +
    "12. Nếu người dùng không cung cấp thông tin cụ thể, " +
    "không tự ý tạo ra thông tin có tính thực tế.\n\n" +
    "Lưu ý:\n" +
    "- Nếu template có placeholder NGAY, hãy điền ngày vào trường đó.\n" +
    "- Nếu template có placeholder THANG, hãy điền tháng vào trường đó.\n" +
    "- Nếu template có placeholder NAM, hãy điền năm vào trường đó.\n" +
    "- Nếu template có placeholder NOI_DUNG, có thể tạo nội dung gồm nhiều câu và nhiều đoạn.\n" +
    "- Không thêm ký hiệu Markdown như **, ## hoặc ```.\n\n" +
    "Kết quả phải tuân thủ chính xác JSON Schema.";

        // -----------------------------------------------------
        // Tạo request body.
        // -----------------------------------------------------

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
                            text = prompt
                        }
                    }
                }
            },

            generationConfig = new
            {
                temperature = 0.3,

                responseMimeType =
                    "application/json",

                responseSchema = schema
            }
        };

        // -----------------------------------------------------
        // Gọi Gemini.
        // -----------------------------------------------------

        var responseContent =
            await SendGeminiRequestAsync(
                modelName,
                apiKey,
                requestBody
            );

        // -----------------------------------------------------
        // Lấy JSON text Gemini trả về.
        // -----------------------------------------------------

        var generatedText =
            ExtractGeneratedText(
                responseContent
            );

        // -----------------------------------------------------
        // Parse JSON thành Dictionary.
        // -----------------------------------------------------

        Dictionary<string, string>? result;

        try
        {
            result =
                JsonSerializer.Deserialize<
                    Dictionary<string, string>
                >(
                    generatedText
                );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini trả về JSON không hợp lệ.",
                ex
            );
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                "Không thể chuyển kết quả Gemini thành Dictionary."
            );
        }

        // -----------------------------------------------------
        // Kiểm tra tất cả placeholder.
        // -----------------------------------------------------

        foreach (var placeholder in placeholders)
        {
            if (!result.ContainsKey(placeholder))
            {
                throw new InvalidOperationException(
                    $"Gemini không trả về placeholder: {placeholder}"
                );
            }

            // Không cho phép giá trị null.
            result[placeholder] ??=
                string.Empty;
        }

        // -----------------------------------------------------
        // Không cho phép Gemini tự ý thêm placeholder.
        // -----------------------------------------------------

        var unexpectedKeys =
            result.Keys
                .Where(
                    key =>
                        !placeholders.Contains(
                            key,
                            StringComparer.Ordinal
                        )
                )
                .ToList();

        if (unexpectedKeys.Count > 0)
        {
            throw new InvalidOperationException(
                "Gemini trả về placeholder không tồn tại trong template: " +
                string.Join(
                    ", ",
                    unexpectedKeys
                )
            );
        }

        return result;
    }

    // =========================================================
    // 3. GỌI GEMINI API
    // =========================================================

    /// <summary>
    /// Gửi request tới Gemini API.
    ///
    /// Có retry đối với:
    /// - HTTP 408
    /// - HTTP 429
    /// - HTTP 5xx
    ///
    /// Thời gian retry:
    /// - Lần 1: 1 giây
    /// - Lần 2: 2 giây
    /// - Lần 3: 4 giây
    /// </summary>
    private async Task<string> SendGeminiRequestAsync(
        string modelName,
        string apiKey,
        object requestBody)
    {
        // -----------------------------------------------------
        // URL Gemini API.
        // -----------------------------------------------------

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

        // -----------------------------------------------------
        // Serialize request body.
        // -----------------------------------------------------

        var json =
            JsonSerializer.Serialize(
                requestBody
            );

        // -----------------------------------------------------
        // Số lần thử tối đa.
        // -----------------------------------------------------

        const int maxRetries = 3;

        // -----------------------------------------------------
        // Retry loop.
        // -----------------------------------------------------

        for (
            var attempt = 1;
            attempt <= maxRetries;
            attempt++)
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    url
                );

            // -------------------------------------------------
            // API key.
            // -------------------------------------------------

            request.Headers.Add(
                "x-goog-api-key",
                apiKey
            );

            // -------------------------------------------------
            // Request body.
            // -------------------------------------------------

            request.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

            // -------------------------------------------------
            // Gửi request.
            // -------------------------------------------------

            using var response =
                await _httpClient.SendAsync(
                    request
                );

            var responseContent =
                await response.Content
                    .ReadAsStringAsync();

            // -------------------------------------------------
            // Thành công.
            // -------------------------------------------------

            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }

            // -------------------------------------------------
            // Xác định có retry hay không.
            // -------------------------------------------------

            var shouldRetry =
                response.StatusCode ==
                    HttpStatusCode.TooManyRequests
                ||
                response.StatusCode ==
                    HttpStatusCode.RequestTimeout
                ||
                (int)response.StatusCode >= 500;

            // -------------------------------------------------
            // Lỗi không thể retry.
            // -------------------------------------------------

            if (!shouldRetry)
            {
                throw new InvalidOperationException(
                    $"Gemini API trả về HTTP " +
                    $"{(int)response.StatusCode}: " +
                    responseContent
                );
            }

            // -------------------------------------------------
            // Nếu còn lượt retry.
            // -------------------------------------------------

            if (attempt < maxRetries)
            {
                // 1 → 2 → 4 giây.
                var delaySeconds =
                    Math.Pow(
                        2,
                        attempt - 1
                    );

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        delaySeconds
                    )
                );
            }
            else
            {
                // -------------------------------------------------
                // Hết số lần retry.
                // -------------------------------------------------

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

    // =========================================================
    // 4. ĐỌC TEXT TỪ GEMINI RESPONSE
    // =========================================================

    /// <summary>
    /// Lấy nội dung text từ response JSON của Gemini.
    /// </summary>
    private static string ExtractGeneratedText(
        string responseContent)
    {
        // -----------------------------------------------------
        // Parse JSON.
        // -----------------------------------------------------

        using var jsonDocument =
            JsonDocument.Parse(
                responseContent
            );

        var root =
            jsonDocument.RootElement;

        // -----------------------------------------------------
        // Kiểm tra candidates.
        // -----------------------------------------------------

        if (!root.TryGetProperty(
                "candidates",
                out var candidates)
            ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini không trả về candidate."
            );
        }

        // -----------------------------------------------------
        // Lấy candidate đầu tiên.
        // -----------------------------------------------------

        var candidate =
            candidates[0];

        // -----------------------------------------------------
        // Kiểm tra content.
        // -----------------------------------------------------

        if (!candidate.TryGetProperty(
                "content",
                out var content))
        {
            throw new InvalidOperationException(
                "Gemini không trả về content."
            );
        }

        // -----------------------------------------------------
        // Kiểm tra parts.
        // -----------------------------------------------------

        if (!content.TryGetProperty(
                "parts",
                out var parts)
            ||
            parts.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini không trả về phần nội dung."
            );
        }

        // -----------------------------------------------------
        // Lấy text.
        // -----------------------------------------------------

        if (!parts[0].TryGetProperty(
                "text",
                out var textElement))
        {
            throw new InvalidOperationException(
                "Gemini không trả về text."
            );
        }

        var text =
            textElement.GetString();

        // -----------------------------------------------------
        // Kiểm tra text rỗng.
        // -----------------------------------------------------

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini trả về nội dung rỗng."
            );
        }

        return text;
    }

    // =========================================================
    // 5. LẤY API KEY
    // =========================================================

    /// <summary>
    /// Lấy Gemini API Key từ Configuration/User Secrets.
    /// </summary>
    private string GetApiKey()
    {
        var apiKey =
            _configuration[
                "Gemini:ApiKey"
            ];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình Gemini API Key."
            );
        }

        return apiKey;
    }

    // =========================================================
    // 6. LẤY MODEL
    // =========================================================

    /// <summary>
    /// Lấy tên model Gemini từ Configuration/User Secrets.
    /// </summary>
    private string GetModelName()
    {
        var modelName =
            _configuration[
                "Gemini:Model"
            ];

        if (string.IsNullOrWhiteSpace(modelName))
        {
            // Giữ model mặc định mà project
            // đang sử dụng.
            return "gemini-3.6-flash";
        }

        return modelName;
    }
}