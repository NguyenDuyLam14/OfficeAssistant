using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Lỗi khi Gmail từ chối gửi thư.
/// StatusCode: 401/403 (cần kết nối lại), 429 (hạn mức), 404, 400...
/// </summary>
public sealed class GmailSendException : Exception
{
    public int StatusCode { get; }

    public GmailSendException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed record GmailSendRequest(
    string To,
    string? Cc,
    string Subject,
    string Body,
    string? ReplyToMessageId);

/// <summary>
/// Tạo thư MIME (UTF-8, tiêu đề mã hóa RFC 2047) và gửi qua Gmail API.
/// Dùng chung cho gửi thư đơn và gửi hàng loạt.
/// </summary>
public sealed class GmailSender
{
    private const string SendUrl =
        "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    private readonly GmailMessageReader _reader;

    public GmailSender(GmailMessageReader reader)
    {
        _reader = reader;
    }

    /// <summary>Gửi thư, trả về ID thư Gmail.</summary>
    public async Task<string?> SendAsync(
        int userId,
        GmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var to = NormalizeAddresses(request.To, required: true, max: 20);
        var cc = NormalizeAddresses(request.Cc, required: false, max: 20);
        var subject = SingleLine(request.Subject, 300);

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Tiêu đề thư không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Nội dung thư không được để trống.");
        }

        string? threadId = null;
        string? inReplyTo = null;
        string? references = null;

        if (!string.IsNullOrWhiteSpace(request.ReplyToMessageId))
        {
            (threadId, inReplyTo, references) =
                await GetReplyContextAsync(
                    userId, request.ReplyToMessageId, cancellationToken);
        }

        var raw = BuildMime(
            to, cc, subject, request.Body, inReplyTo, references);

        var payload = new Dictionary<string, string> { ["raw"] = raw };

        if (!string.IsNullOrEmpty(threadId))
        {
            payload["threadId"] = threadId;
        }

        using var response = await _reader.SendGmailPostAsync(
            userId,
            SendUrl,
            JsonSerializer.Serialize(payload),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        return document.RootElement.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    // =========================================================
    // NGỮ CẢNH TRẢ LỜI (threadId, Message-ID)
    // =========================================================

    private async Task<(string? ThreadId, string? InReplyTo, string? References)>
        GetReplyContextAsync(
            int userId,
            string messageId,
            CancellationToken cancellationToken)
    {
        if (messageId.Length > 200 ||
            messageId.Any(c =>
                !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
        {
            throw new ArgumentException("Mã email gốc không hợp lệ.");
        }

        var url =
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/" +
            Uri.EscapeDataString(messageId) +
            "?format=metadata&metadataHeaders=Message-ID" +
            "&metadataHeaders=References";

        using var response = await _reader.SendGmailRequestAsync(
            userId, url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new GmailSendException(
                404, "Không tìm thấy email gốc để trả lời.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        string? threadId = root.TryGetProperty("threadId", out var t)
            ? t.GetString()
            : null;

        string? originalId = null;
        string? originalRefs = null;

        if (root.TryGetProperty("payload", out var payload) &&
            payload.TryGetProperty("headers", out var headers) &&
            headers.ValueKind == JsonValueKind.Array)
        {
            foreach (var header in headers.EnumerateArray())
            {
                var name = header.TryGetProperty("name", out var n)
                    ? n.GetString()
                    : null;

                var value = header.TryGetProperty("value", out var v)
                    ? v.GetString()
                    : null;

                if (string.Equals(name, "Message-ID",
                        StringComparison.OrdinalIgnoreCase))
                {
                    originalId = StripLineBreaks(value);
                }
                else if (string.Equals(name, "References",
                        StringComparison.OrdinalIgnoreCase))
                {
                    originalRefs = StripLineBreaks(value);
                }
            }
        }

        var references = string.Join(" ",
            new[] { originalRefs, originalId }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return (
            threadId,
            originalId,
            string.IsNullOrWhiteSpace(references) ? null : references);
    }

    // =========================================================
    // TẠO THƯ MIME
    // =========================================================

    private static string BuildMime(
        string to,
        string? cc,
        string subject,
        string body,
        string? inReplyTo,
        string? references)
    {
        var sb = new StringBuilder();

        sb.Append("To: ").Append(to).Append("\r\n");

        if (!string.IsNullOrEmpty(cc))
        {
            sb.Append("Cc: ").Append(cc).Append("\r\n");
        }

        sb.Append("Subject: ").Append(EncodeHeader(subject)).Append("\r\n");

        if (!string.IsNullOrWhiteSpace(inReplyTo))
        {
            sb.Append("In-Reply-To: ").Append(inReplyTo).Append("\r\n");
        }

        if (!string.IsNullOrWhiteSpace(references))
        {
            sb.Append("References: ").Append(references).Append("\r\n");
        }

        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Content-Type: text/plain; charset=\"UTF-8\"\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n");
        sb.Append("\r\n");

        // Chuẩn hóa xuống dòng về CRLF rồi mã hóa base64 (76 ký tự/dòng).
        var normalizedBody = body
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", "\r\n");

        sb.Append(Convert.ToBase64String(
            Encoding.UTF8.GetBytes(normalizedBody),
            Base64FormattingOptions.InsertLineBreaks));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Mã hóa tiêu đề theo RFC 2047 khi có ký tự không phải ASCII
    /// (tiếng Việt). Cắt thành nhiều encoded-word để không quá dài.
    /// </summary>
    private static string EncodeHeader(string value)
    {
        if (value.All(c => c < 128))
        {
            return value;
        }

        var words = new List<string>();
        var current = new StringBuilder();
        var bytes = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            var text = rune.ToString();
            var size = Encoding.UTF8.GetByteCount(text);

            if (bytes + size > 45)
            {
                words.Add(current.ToString());
                current.Clear();
                bytes = 0;
            }

            current.Append(text);
            bytes += size;
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return string.Join("\r\n ", words.Select(word =>
            "=?UTF-8?B?" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(word)) +
            "?="));
    }

    // =========================================================
    // KIỂM TRA ĐẦU VÀO (chống chèn header)
    // =========================================================

    /// <summary>Bỏ xuống dòng khỏi chuỗi dùng làm header.</summary>
    public static string SingleLine(string? value, int max)
    {
        var text = StripLineBreaks(value)?.Trim() ?? string.Empty;
        return text.Length > max ? text[..max] : text;
    }

    private static string? StripLineBreaks(string? value) =>
        value?.Replace("\r", " ").Replace("\n", " ");

    private static string NormalizeAddresses(
        string? input, bool required, int max)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return required
                ? throw new ArgumentException(
                    "Vui lòng nhập địa chỉ người nhận.")
                : string.Empty;
        }

        var addresses = new List<string>();

        foreach (var part in input.Split(
                     new[] { ',', ';' },
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (!MailAddress.TryCreate(part, out var address) ||
                !address.Address.Contains('.') ||
                address.Address.Length > 255)
            {
                throw new ArgumentException(
                    $"Địa chỉ email không hợp lệ: {part}");
            }

            addresses.Add(address.Address);
        }

        if (addresses.Count == 0 && required)
        {
            throw new ArgumentException("Vui lòng nhập địa chỉ người nhận.");
        }

        if (addresses.Count > max)
        {
            throw new ArgumentException(
                $"Tối đa {max} người nhận cho một thư.");
        }

        return string.Join(", ", addresses);
    }

    // =========================================================
    // PHÂN TÍCH LỖI GMAIL
    // =========================================================

    private static async Task<GmailSendException> BuildExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        string? reason = null;

        try
        {
            var text = await response.Content
                .ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(text);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0 &&
                errors[0].TryGetProperty("reason", out var r))
            {
                reason = r.GetString();
            }
        }
        catch (JsonException)
        {
            // Không đọc được chi tiết lỗi thì dùng mã HTTP.
        }

        // Các lỗi hạn mức/tốc độ của Gmail quy về 429.
        if (status == 429 ||
            (reason != null &&
             (reason.Contains("ateLimit", StringComparison.OrdinalIgnoreCase) ||
              reason.Contains("dailyLimit", StringComparison.OrdinalIgnoreCase) ||
              reason.Contains("quota", StringComparison.OrdinalIgnoreCase))))
        {
            status = 429;
        }

        var message = status switch
        {
            429 => "Gmail đang giới hạn tốc độ hoặc hạn mức gửi thư.",
            403 => "Tài khoản Gmail chưa cấp quyền gửi thư. Hãy kết nối lại Gmail.",
            401 => "Quyền truy cập Gmail không còn hiệu lực. Hãy kết nối lại Gmail.",
            400 => "Gmail không chấp nhận thư này (địa chỉ hoặc nội dung không hợp lệ).",
            _ => "Không thể gửi thư qua Gmail."
        };

        return new GmailSendException(status, message);
    }
}
