using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using OfficeAssistant.API.DTOs.Email;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Đọc danh sách người nhận từ file .xlsx/.xlsm/.csv.
/// Dòng đầu là tiêu đề; cột có chữ "mail" là email;
/// cột "Tên/Họ tên/Name" (nếu có) là tên người nhận.
/// Mọi cột đều dùng được làm biến: {{Tên cột}}.
/// </summary>
public static class RecipientFileParser
{
    private const int MaxFileRows = 5000;
    private const int MaxColumns = 50;
    private const int MaxCellLength = 500;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s,;<>]+@[^@\s,;<>]+\.[^@\s,;<>]+$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> NameHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ten", "tên", "họ tên", "họ và tên", "ho ten", "ho va ten",
            "name", "full name", "fullname"
        };

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Length <= 255 &&
        EmailRegex.IsMatch(email);

    public static ParsedRecipients Parse(
        Stream stream, string extension, int maxRecipients)
    {
        var table = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(stream)
            : ReadExcel(stream);

        if (table.Count < 2)
        {
            throw new InvalidDataException(
                "File cần có dòng tiêu đề và ít nhất một dòng dữ liệu.");
        }

        var headers = BuildHeaders(table[0]);

        var emailColumn = headers.FindIndex(h =>
            h.Contains("mail", StringComparison.OrdinalIgnoreCase));

        if (emailColumn < 0)
        {
            throw new InvalidDataException(
                "Không tìm thấy cột email. Tiêu đề cột phải chứa chữ \"email\".");
        }

        var nameColumn = headers.FindIndex(h => NameHeaders.Contains(h));

        var recipients = new List<RecipientInput>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var totalRows = 0;
        var invalid = 0;
        var duplicates = 0;
        var truncated = false;

        for (var r = 1; r < table.Count; r++)
        {
            var row = table[r];

            string Cell(int index) =>
                index >= 0 && index < row.Count
                    ? Truncate(row[index].Trim(), MaxCellLength)
                    : string.Empty;

            // Bỏ qua dòng hoàn toàn trống.
            if (row.All(cell => string.IsNullOrWhiteSpace(cell)))
            {
                continue;
            }

            totalRows++;

            var email = Cell(emailColumn);

            if (!IsValidEmail(email))
            {
                invalid++;
                continue;
            }

            if (!seen.Add(email))
            {
                duplicates++;
                continue;
            }

            if (recipients.Count >= maxRecipients)
            {
                truncated = true;
                break;
            }

            var fields = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headers.Count; i++)
            {
                fields.TryAdd(headers[i], Cell(i));
            }

            var name = nameColumn >= 0 ? Cell(nameColumn) : string.Empty;

            fields.TryAdd("Email", email);
            fields.TryAdd("Ten", name);

            recipients.Add(new RecipientInput
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Fields = fields
            });
        }

        var variables = headers
            .Append("Ten")
            .Append("Email")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ParsedRecipients(
            variables, recipients, totalRows, invalid, duplicates, truncated);
    }

    // =========================================================
    // TIÊU ĐỀ CỘT
    // =========================================================

    private static List<string> BuildHeaders(List<string> row)
    {
        var headers = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < Math.Min(row.Count, MaxColumns); i++)
        {
            var name = row[i].Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = $"Cột {i + 1}";
            }

            var unique = name;
            var suffix = 2;

            while (!used.Add(unique))
            {
                unique = $"{name} ({suffix++})";
            }

            headers.Add(Truncate(unique, 100));
        }

        return headers;
    }

    // =========================================================
    // ĐỌC EXCEL
    // =========================================================

    private static List<List<string>> ReadExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.First();
        var used = worksheet.RangeUsed();

        if (used == null)
        {
            throw new InvalidDataException("Sheet không có dữ liệu.");
        }

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = Math.Min(
            used.LastRow().RowNumber(), firstRow + MaxFileRows);
        var firstColumn = used.FirstColumn().ColumnNumber();
        var lastColumn = Math.Min(
            used.LastColumn().ColumnNumber(), firstColumn + MaxColumns - 1);

        var rows = new List<List<string>>();

        for (var r = firstRow; r <= lastRow; r++)
        {
            var row = new List<string>();

            for (var c = firstColumn; c <= lastColumn; c++)
            {
                row.Add(worksheet.Cell(r, c).GetFormattedString());
            }

            rows.Add(row);
        }

        return rows;
    }

    // =========================================================
    // ĐỌC CSV (dấu phân cách , hoặc ;, hỗ trợ ngoặc kép)
    // =========================================================

    private static List<List<string>> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var text = reader.ReadToEnd();

        var firstLineEnd = text.IndexOf('\n');
        var firstLine = firstLineEnd < 0 ? text : text[..firstLineEnd];

        var delimiter =
            firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',')
                ? ';'
                : ',';

        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\r')
            {
                // Bỏ qua, xử lý ở '\n'.
            }
            else if (c == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();

                if (rows.Count > MaxFileRows)
                {
                    return rows;
                }
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
