
using System.Text;
using System.Text.Json;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Trích xuất tiêu đề, người gửi, ngày gửi và nội dung văn bản
/// từ JSON đầy đủ do Gmail API trả về.
/// Không thực thi hoặc hiển thị HTML của email.
/// </summary>
public sealed class GmailEmailContentExtractor
{
    public GmailEmailContent Extract(JsonDocument document)
    {
        var root = document.RootElement;

        var payload = GetProperty(root, "payload");

        var headers = GetProperty(payload, "headers");

        var subject = GetHeader(headers, "Subject");
        var sender = GetHeader(headers, "From");
        var date = GetHeader(headers, "Date");

        var bodyBuilder = new StringBuilder();

        ExtractPlainText(GetProperty(payload, "body"), bodyBuilder);

        ExtractParts(GetProperty(payload, "parts"), bodyBuilder);

        var body = bodyBuilder.ToString().Trim();

        // Giới hạn kích thước nội dung để tránh gửi email quá dài tới AI.
        const int maxBodyLength = 12000;

        if (body.Length > maxBodyLength)
        {
            body = body[..maxBodyLength];
        }

        return new GmailEmailContent
        {
            Subject = subject,
            Sender = sender,
            Date = date,
            Body = body
        };
    }

    private static void ExtractParts(
        JsonElement parts,
        StringBuilder output)
    {
        if (parts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var part in parts.EnumerateArray())
        {
            var mimeType = GetString(part, "mimeType");

            // Ưu tiên text/plain, không lấy nội dung text/html.
            if (string.Equals(
                mimeType,
                "text/plain",
                StringComparison.OrdinalIgnoreCase))
            {
                ExtractPlainText(GetProperty(part, "body"), output);
            }

            // Duyệt các phần lồng nhau, ví dụ multipart/alternative.
            ExtractParts(GetProperty(part, "parts"), output);
        }
    }

    private static void ExtractPlainText(
        JsonElement body,
        StringBuilder output)
    {
        var data = GetString(body, "data");

        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        try
        {
            var bytes = DecodeBase64Url(data);
            var text = Encoding.UTF8.GetString(bytes);

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (output.Length > 0)
                {
                    output.AppendLine();
                }

                output.Append(text);
            }
        }
        catch (FormatException)
        {
            // Bỏ qua phần dữ liệu lỗi, không làm hỏng toàn bộ email.
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }

    private static string GetHeader(
        JsonElement headers,
        string name)
    {
        if (headers.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var header in headers.EnumerateArray())
        {
            var headerName = GetString(header, "name");

            if (string.Equals(
                headerName,
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                return GetString(header, "value");
            }
        }

        return string.Empty;
    }

    private static JsonElement GetProperty(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value))
        {
            return value;
        }

        return default;
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        var value = GetProperty(element, propertyName);

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}

/// <summary>
/// Dữ liệu email đã trích xuất để chuyển cho bước phân tích AI.
/// </summary>
public sealed class GmailEmailContent
{
    public string Subject { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
