using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services.Email;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Cung cấp API đọc danh sách và nội dung email Gmail.GetMessages()
/// Mỗi người dùng chỉ được sử dụng kết nối Gmail của chính mình.
/// </summary>
[ApiController]
[Route("api/Emails")]
[Authorize]
public sealed class GmailMessagesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly GmailOAuthService _oauthService;
    private readonly GmailTokenProtector _tokenProtector;
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailMessagesController(
        ApplicationDbContext db,
        GmailOAuthService oauthService,
        GmailTokenProtector tokenProtector,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _oauthService = oauthService;
        _tokenProtector = tokenProtector;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Lấy UserId từ JWT của người đang đăng nhập.
    /// Không nhận UserId từ query string để tránh truy cập hộp thư của người khác.
    /// </summary>
    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out userId);
    }

    /// <summary>
    /// Lấy access token còn hiệu lực.
    /// Nếu token sắp hết hạn, tự động làm mới bằng refresh token.
    /// </summary>
    private async Task<string> GetValidAccessTokenAsync(
        int userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await _db.GmailConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (connection == null)
        {
            throw new GmailReconnectRequiredException(
                "Bạn chưa kết nối Gmail. Hãy kết nối Gmail trước khi đọc email.");
        }

        // Làm mới trước 5 phút để giảm nguy cơ token hết hạn giữa chừng.
        var tokenStillValid =
            connection.TokenExpiresAtUtc.HasValue &&
            connection.TokenExpiresAtUtc.Value >
                DateTime.UtcNow.AddMinutes(5);

        if (!forceRefresh && tokenStillValid)
        {
            try
            {
                return _tokenProtector.Unprotect(connection.AccessToken);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new GmailReconnectRequiredException(
                    "Không thể giải mã thông tin kết nối Gmail. Vui lòng kết nối lại.");
            }
        }

        if (string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            throw new GmailReconnectRequiredException(
                "Kết nối Gmail không có refresh token. Vui lòng kết nối lại Gmail.");
        }

        string refreshToken;

        try
        {
            refreshToken = _tokenProtector.Unprotect(
                connection.RefreshToken);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new GmailReconnectRequiredException(
                "Không thể giải mã refresh token. Vui lòng kết nối lại Gmail.");
        }

        GoogleOAuthTokenResponse refreshed;

        try
        {
            refreshed = await _oauthService.RefreshAccessTokenAsync(
                refreshToken,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            // Không trả thông tin xác thực hoặc nội dung lỗi nội bộ ra client.
            throw new GmailReconnectRequiredException(
                "Không thể làm mới quyền truy cập Gmail. " +
                "Vui lòng kiểm tra kết nối hoặc kết nối lại Gmail.");
        }

        // Google thường không trả refresh token mới trong lần làm mới.
        // Vì vậy giữ nguyên refresh token đang có.
        connection.AccessToken =
            _tokenProtector.Protect(refreshed.AccessToken);

        connection.TokenExpiresAtUtc =
            DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);

        connection.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return refreshed.AccessToken;
    }

    /// <summary>
    /// Gửi yêu cầu đến Gmail API.
    /// Nếu Gmail trả HTTP 401, thử làm mới token và gửi lại một lần.
    /// </summary>
    private async Task<HttpResponseMessage> SendGmailRequestAsync(
        int userId,
        string url,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetValidAccessTokenAsync(
            userId,
            cancellationToken: cancellationToken);

        var client = _httpClientFactory.CreateClient();

        var response = await SendWithTokenAsync(
            client, url, accessToken, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Access token có thể bị Google thu hồi trước thời điểm hết hạn.
        response.Dispose();

        accessToken = await GetValidAccessTokenAsync(
            userId,
            forceRefresh: true,
            cancellationToken: cancellationToken);

        return await SendWithTokenAsync(
            client, url, accessToken, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client,
        string url,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

        return await client.SendAsync(request, cancellationToken);
    }

    private IActionResult ReconnectRequired(string message)
    {
        // Dùng 409 thay vì 401 để frontend không nhầm với JWT hết hạn.
        return Conflict(new
        {
            code = "gmail_reconnect_required",
            message
        });
    }

    private IActionResult GmailFailure(
        HttpResponseMessage response,
        string message)
    {
        var status = (int)response.StatusCode;

        // Google từ chối token: cần kết nối lại, không phải lỗi đăng nhập app.
        if (status == 401)
        {
            return ReconnectRequired(
                "Quyền truy cập Gmail không còn hiệu lực. Vui lòng kết nối lại Gmail.");
        }

        return StatusCode(status, new
        {
            message,
            gmailStatusCode = status
        });
    }

    /// <summary>
    /// GET /api/Emails/messages?maxResults=20&q=
    /// Lấy danh sách email của Gmail đã kết nối.
    /// </summary>
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] int maxResults = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        if (maxResults < 1 || maxResults > 50)
        {
            return BadRequest(new
            {
                message = "maxResults phải nằm trong khoảng từ 1 đến 50."
            });
        }

        // Kiểm tra độ dài pageToken để tránh nhận dữ liệu quá lớn.
        if (pageToken != null && pageToken.Length > 4096)
        {
            return BadRequest(new
            {
                message = "Mã trang email không hợp lệ."
            });
        }

        try
        {
            // Tham số dùng để yêu cầu Gmail trả về một trang email.
            var parameters = new Dictionary<string, string?>
            {
                ["maxResults"] = maxResults.ToString()
            };

            // Giữ nguyên điều kiện tìm kiếm nếu người dùng đang tìm email.
            if (!string.IsNullOrWhiteSpace(q))
            {
                parameters["q"] = q.Trim();
            }

            // Có pageToken thì lấy trang tiếp theo.
            // Không có pageToken thì Gmail trả về trang đầu tiên.
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                parameters["pageToken"] = pageToken;
            }

            var url = QueryHelpers.AddQueryString(
                "https://gmail.googleapis.com/gmail/v1/users/me/messages",
                parameters);

            using var listResponse = await SendGmailRequestAsync(
                userId, url, cancellationToken);

            if (!listResponse.IsSuccessStatusCode)
            {
                return GmailFailure(
                    listResponse,
                    "Không thể lấy danh sách email từ Gmail.");
            }

            using var listStream =
                await listResponse.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var listDocument =
                await JsonDocument.ParseAsync(
                    listStream,
                    cancellationToken: cancellationToken);

            var root = listDocument.RootElement;
            var messages = new List<object>();

            if (root.TryGetProperty("messages", out var messageArray) &&
                messageArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in messageArray.EnumerateArray())
                {
                    if (!item.TryGetProperty("id", out var idElement))
                    {
                        continue;
                    }

                    var messageId = idElement.GetString();

                    if (string.IsNullOrWhiteSpace(messageId))
                    {
                        continue;
                    }

                    // Gmail trả ID trước; gọi tiếp để lấy tiêu đề,
                    // người gửi và ngày gửi của từng email.
                    var metadataUrl =
                        "https://gmail.googleapis.com/gmail/v1/users/me/messages/" +
                        Uri.EscapeDataString(messageId) +
                        "?format=metadata" +
                        "&metadataHeaders=From" +
                        "&metadataHeaders=Subject" +
                        "&metadataHeaders=Date";

                    using var metadataResponse =
                        await SendGmailRequestAsync(
                            userId, metadataUrl, cancellationToken);

                    if (!metadataResponse.IsSuccessStatusCode)
                    {
                        // Bỏ qua email riêng lẻ bị lỗi, tiếp tục xử lý email khác.
                        continue;
                    }

                    using var metadataStream =
                        await metadataResponse.Content.ReadAsStreamAsync(
                            cancellationToken);

                    using var metadataDocument =
                        await JsonDocument.ParseAsync(
                            metadataStream,
                            cancellationToken: cancellationToken);

                    var metadata = metadataDocument.RootElement;

                    var headers = FindHeaders(metadata);

                    messages.Add(new
                    {
                        id = messageId,
                        threadId = GetString(metadata, "threadId"),
                        snippet = GetString(metadata, "snippet"),
                        from = GetHeader(headers, "From"),
                        subject = GetHeader(headers, "Subject"),
                        date = GetHeader(headers, "Date"),
                        internalDate = GetString(metadata, "internalDate"),
                        labelIds = GetStringArray(metadata, "labelIds")
                    });
                }
            }

            return Ok(new
            {
                messages,
                resultCount = messages.Count,
                nextPageToken = GetString(root, "nextPageToken")
            });
        }
        catch (GmailReconnectRequiredException ex)
        {
            return ReconnectRequired(ex.Message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                message = "Không thể kết nối đến dịch vụ Gmail."
            });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new
            {
                message = "Yêu cầu Gmail quá thời gian chờ."
            });
        }
        catch (JsonException)
        {
            return StatusCode(502, new
            {
                message = "Dữ liệu Gmail trả về không hợp lệ."
            });
        }
    }

    /// <summary>
    /// GET /api/Emails/messages/{messageId}
    /// Lấy chi tiết một email, bao gồm phần nội dung văn bản và HTML.
    /// </summary>
    [HttpGet("messages/{messageId}")]
    public async Task<IActionResult> GetMessage(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đăng nhập."
            });
        }

        if (string.IsNullOrWhiteSpace(messageId) ||
            messageId.Length > 200 ||
            messageId.Any(c =>
                !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
        {
            return BadRequest(new
            {
                message = "Mã email không hợp lệ."
            });
        }

        try
        {
            var url =
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/" +
                Uri.EscapeDataString(messageId) +
                "?format=full";

            using var response = await SendGmailRequestAsync(
                userId, url, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy email trong Gmail đã kết nối."
                });
            }

            if (!response.IsSuccessStatusCode)
            {
                return GmailFailure(
                    response,
                    "Không thể lấy nội dung email từ Gmail.");
            }

            using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            var root = document.RootElement;
            var headers = FindHeaders(root);

            string? plainTextBody = null;
            string? htmlBody = null;

            if (root.TryGetProperty("payload", out var payload))
            {
                ExtractBodies(payload, ref plainTextBody, ref htmlBody);
            }

            return Ok(new
            {
                id = GetString(root, "id"),
                threadId = GetString(root, "threadId"),
                snippet = GetString(root, "snippet"),
                from = GetHeader(headers, "From"),
                to = GetHeader(headers, "To"),
                cc = GetHeader(headers, "Cc"),
                subject = GetHeader(headers, "Subject"),
                date = GetHeader(headers, "Date"),
                internalDate = GetString(root, "internalDate"),
                labelIds = GetStringArray(root, "labelIds"),
                plainTextBody,
                htmlBody
            });
        }
        catch (GmailReconnectRequiredException ex)
        {
            return ReconnectRequired(ex.Message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                message = "Không thể kết nối đến dịch vụ Gmail."
            });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new
            {
                message = "Yêu cầu Gmail quá thời gian chờ."
            });
        }
        catch (JsonException)
        {
            return StatusCode(502, new
            {
                message = "Dữ liệu Gmail trả về không hợp lệ."
            });
        }
    }

    // Tìm danh sách header trong payload hoặc thông tin email.
    private static JsonElement FindHeaders(JsonElement element)
    {
        if (element.TryGetProperty("payload", out var payload) &&
            payload.TryGetProperty("headers", out var payloadHeaders))
        {
            return payloadHeaders;
        }

        if (element.TryGetProperty("headers", out var headers))
        {
            return headers;
        }

        return default;
    }

    private static string? GetHeader(
        JsonElement headers,
        string headerName)
    {
        if (headers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var header in headers.EnumerateArray())
        {
            var name = GetString(header, "name");

            if (string.Equals(
                name, headerName, StringComparison.OrdinalIgnoreCase))
            {
                return GetString(header, "value");
            }
        }

        return null;
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string[] GetStringArray(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return array.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .ToArray();
    }

    /// <summary>
    /// Duyệt payload đệ quy vì email có thể chứa nhiều phần MIME.
    /// Ưu tiên lấy text/plain; nếu có, vẫn giữ htmlBody riêng.
    /// </summary>
    private static void ExtractBodies(
        JsonElement part,
        ref string? plainTextBody,
        ref string? htmlBody)
    {
        var mimeType = GetString(part, "mimeType");

        if (part.TryGetProperty("body", out var body))
        {
            var data = GetString(body, "data");

            if (!string.IsNullOrWhiteSpace(data))
            {
                var decoded = DecodeBase64Url(data);

                if (string.Equals(
                    mimeType, "text/plain",
                    StringComparison.OrdinalIgnoreCase))
                {
                    plainTextBody ??= decoded;
                }
                else if (string.Equals(
                    mimeType, "text/html",
                    StringComparison.OrdinalIgnoreCase))
                {
                    htmlBody ??= decoded;
                }
            }
        }

        if (part.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in parts.EnumerateArray())
            {
                ExtractBodies(child, ref plainTextBody, ref htmlBody);
            }
        }
    }

    // Gmail dùng Base64 URL-safe: '-' và '_' thay cho '+' và '/'.
    private static string DecodeBase64Url(string value)
    {
        var base64 = value
            .Replace('-', '+')
            .Replace('_', '/');

        base64 = base64.PadRight(
            base64.Length + ((4 - base64.Length % 4) % 4), '=');

        return Encoding.UTF8.GetString(
            Convert.FromBase64String(base64));
    }

    private sealed class GmailReconnectRequiredException : Exception
    {
        public GmailReconnectRequiredException(string message)
            : base(message)
        {
        }
    }
}
