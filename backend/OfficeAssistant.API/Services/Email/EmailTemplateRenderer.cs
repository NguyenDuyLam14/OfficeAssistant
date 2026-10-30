using System.Text.Json;
using System.Text.RegularExpressions;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Điền biến {{Ten}}, {{Email}}, {{Tên cột Excel}} vào mẫu thư.
/// Tên biến không phân biệt hoa thường. Biến không có giá trị -> chuỗi rỗng.
/// </summary>
public static class EmailTemplateRenderer
{
    private static readonly Regex VariableRegex =
        new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractVariables(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return Array.Empty<string>();
        }

        return VariableRegex.Matches(template)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Render(
        string? template,
        IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return VariableRegex.Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value.Trim(), out var value)
                ? value
                : string.Empty);
    }

    /// <summary>Gom giá trị biến của một người nhận.</summary>
    public static Dictionary<string, string> BuildValues(
        string email, string? name, string? dataJson)
    {
        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            try
            {
                var data = JsonSerializer
                    .Deserialize<Dictionary<string, string>>(dataJson);

                if (data != null)
                {
                    foreach (var item in data)
                    {
                        values[item.Key] = item.Value ?? string.Empty;
                    }
                }
            }
            catch (JsonException)
            {
                // Dữ liệu biến lỗi: bỏ qua, chỉ dùng Email/Ten.
            }
        }

        values.TryAdd("Email", email);
        values.TryAdd("Ten", name ?? string.Empty);

        return values;
    }
}
