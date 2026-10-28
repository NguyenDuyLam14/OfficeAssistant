using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Globalization;
using System.Security.Claims;
using System.Text;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.AI;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// Nhập dữ liệu từ file Excel để thống kê và trực quan hóa.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ExcelController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private const int MaxRows = 20000;
    private const int MaxColumns = 100;

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;

    public ExcelController(
        ApplicationDbContext context,
        IAIService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out userId) && userId > 0;
    }


    /// <summary>
    /// POST api/Excel/analyze?sheetName=...
    /// Form-data: file = file .xlsx
    /// </summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Analyze(
    [FromForm] IFormFile file,
    [FromQuery] string? sheetName = null,
    [FromQuery] bool save = true,
    CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra file
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file Excel." });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { message = "File không được vượt quá 10 MB." });
        }

        var extension = Path.GetExtension(file.FileName);

        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Chỉ hỗ trợ file Excel định dạng .xlsx (hoặc .xlsm)."
            });
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();

            // 2. Chọn sheet
            var worksheet = string.IsNullOrWhiteSpace(sheetName)
                ? workbook.Worksheets.First()
                : workbook.Worksheets.FirstOrDefault(w =>
                    w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

            if (worksheet == null)
            {
                return BadRequest(new { message = "Không tìm thấy sheet được chọn." });
            }

            var used = worksheet.RangeUsed();

            if (used == null)
            {
                return BadRequest(new { message = "Sheet không có dữ liệu." });
            }

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var firstCol = used.FirstColumn().ColumnNumber();
            var lastCol = Math.Min(
                used.LastColumn().ColumnNumber(),
                firstCol + MaxColumns - 1);

            if (lastRow <= firstRow)
            {
                return BadRequest(new
                {
                    message = "Sheet cần có dòng tiêu đề và ít nhất một dòng dữ liệu."
                });
            }

            // 3. Dòng đầu tiên là tiêu đề cột (đảm bảo không trống/trùng)
            var headers = new List<string>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var c = firstCol; c <= lastCol; c++)
            {
                var name = worksheet.Cell(firstRow, c).GetFormattedString().Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Cột {c - firstCol + 1}";
                }

                var unique = name;
                var suffix = 2;

                while (!usedNames.Add(unique))
                {
                    unique = $"{name} ({suffix++})";
                }

                headers.Add(unique);
            }

            // 4. Đọc dữ liệu
            var rawRows = new List<object?[]>();
            var truncated = false;

            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                var row = new object?[headers.Count];
                var hasValue = false;

                for (var c = firstCol; c <= lastCol; c++)
                {
                    var value = ReadCell(worksheet.Cell(r, c));
                    row[c - firstCol] = value;

                    if (value != null)
                    {
                        hasValue = true;
                    }
                }

                if (!hasValue)
                {
                    continue; // bỏ qua dòng trống
                }

                if (rawRows.Count >= MaxRows)
                {
                    truncated = true;
                    break;
                }

                rawRows.Add(row);
            }

            if (rawRows.Count == 0)
            {
                return BadRequest(new { message = "Không có dòng dữ liệu nào." });
            }

            // 5. Nhận diện kiểu cột + thống kê + chuẩn hóa dữ liệu trả về
            var columns = new List<ColumnStats>();
            var rows = rawRows.Select(_ => new object?[headers.Count]).ToList();

            for (var i = 0; i < headers.Count; i++)
            {
                var values = rawRows.Select(r => r[i]).ToList();
                var present = values.Where(v => v != null).ToList();

                string type;

                if (present.Count > 0 && present.All(v => v is double))
                {
                    type = "Number";
                }
                else if (present.Count > 0 && present.All(v => v is DateTime))
                {
                    type = "Date";
                }
                else
                {
                    type = "Text";
                }

                // Giá trị trả về cho frontend
                for (var r = 0; r < rawRows.Count; r++)
                {
                    rows[r][i] = ToOutput(values[r], type);
                }

                columns.Add(BuildStats(headers[i], type, values));
            }

            // Lưu lịch sử (bỏ qua khi chỉ đổi sheet của cùng một file).
            if (save)
            {
                await SaveHistoryAsync(
                    file.FileName,
                    worksheet.Name,
                    rows.Count,
                    headers.Count,
                    cancellationToken);
            }

            return Ok(new
            {
                fileName = file.FileName,
                sheetName = worksheet.Name,
                sheetNames,
                totalRows = rows.Count,
                truncated,
                maxRows = MaxRows,
                columns,
                rows
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("===== EXCEL ERROR =====");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("=======================");

            return BadRequest(new
            {
                message = "Không đọc được file Excel. File có thể bị lỗi hoặc đặt mật khẩu."
            });
        }
    }

    // =========================================================
    // HÀM HỖ TRỢ
    // =========================================================

    private static object? ReadCell(IXLCell cell)
    {
        var v = cell.Value;

        if (v.IsBlank) return null;
        if (v.IsNumber) return v.GetNumber();
        if (v.IsDateTime) return v.GetDateTime();
        if (v.IsBoolean) return v.GetBoolean() ? "TRUE" : "FALSE";

        if (v.IsText)
        {
            var text = v.GetText().Trim();
            return text.Length == 0 ? null : text;
        }

        return null; // ô lỗi (#DIV/0!, ...)
    }

    private static object? ToOutput(object? value, string type)
    {
        if (value == null) return null;

        return value switch
        {
            double d when type == "Number" => d,
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("yyyy-MM-dd")
                : dt.ToString("yyyy-MM-dd HH:mm"),
            _ => value.ToString()
        };
    }

    private static ColumnStats BuildStats(
        string name, string type, List<object?> values)
    {
        var present = values.Where(v => v != null).ToList();
        var missing = values.Count - present.Count;

        if (type == "Number")
        {
            var numbers = present.Cast<double>().OrderBy(x => x).ToList();
            var sum = numbers.Sum();
            var mean = sum / numbers.Count;

            var median = numbers.Count % 2 == 1
                ? numbers[numbers.Count / 2]
                : (numbers[numbers.Count / 2 - 1] + numbers[numbers.Count / 2]) / 2;

            var variance = numbers.Sum(x => Math.Pow(x - mean, 2)) / numbers.Count;

            return new ColumnStats(
                name, type, numbers.Count, missing,
                numbers.Distinct().Count(),
                Round(sum), Round(mean), Round(numbers[0]),
                Round(numbers[^1]), Round(median),
                Round(Math.Sqrt(variance)),
                null, null, new List<ValueCount>());
        }

        var texts = present
            .Select(v => v is DateTime dt ? dt.ToString("yyyy-MM-dd") : v!.ToString()!)
            .ToList();

        var top = texts
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => new ValueCount(g.Key, g.Count()))
            .ToList();

        string? minText = null;
        string? maxText = null;

        if (type == "Date" && texts.Count > 0)
        {
            minText = texts.Min();
            maxText = texts.Max();
        }

        return new ColumnStats(
            name, type, texts.Count, missing,
            texts.Distinct().Count(),
            null, null, null, null, null, null,
            minText, maxText, top);
    }

    private static double Round(double value) => Math.Round(value, 2);

    // =========================================================
    // LƯU LỊCH SỬ PHÂN TÍCH
    // =========================================================

    private async Task SaveHistoryAsync(
        string fileName,
        string sheetName,
        int totalRows,
        int columnCount,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return;
        }

        try
        {
            _context.ExcelAnalysisRecords.Add(new ExcelAnalysisRecord
            {
                UserId = userId,
                FileName = Truncate(fileName, 300),
                SheetName = Truncate(sheetName, 200),
                TotalRows = totalRows,
                ColumnCount = columnCount,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Lỗi lưu lịch sử không được làm hỏng kết quả phân tích.
            Console.WriteLine("===== EXCEL HISTORY ERROR =====");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("===============================");

            _context.ChangeTracker.Clear();
        }
    }

    // =========================================================
    // POST api/Excel/insights  -  AI nhận xét dữ liệu
    // =========================================================

    [HttpPost("insights")]
    public async Task<IActionResult> Insights(
        ExcelStatsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Columns == null ||
            request.Columns.Count == 0 ||
            request.Columns.Count > MaxColumns)
        {
            return BadRequest(new { message = "Dữ liệu thống kê không hợp lệ." });
        }

        try
        {
            var summary = BuildSummaryText(request);

            var insight = await _aiService.AnalyzeDataAsync(
                summary, cancellationToken);

            return Ok(new { insight });
        }
        catch (GeminiQuotaException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "AI đang tạm hết lượt sử dụng. Vui lòng thử lại sau.",
                retryAfterUtc = ex.RetryAfterUtc
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("===== EXCEL AI ERROR =====");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("==========================");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Đã xảy ra lỗi khi nhờ AI nhận xét dữ liệu."
            });
        }
    }

    // Tạo bản tóm tắt dạng văn bản để gửi cho AI.
    private static string BuildSummaryText(ExcelStatsRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Tên file: {Clean(request.FileName, 150)}");
        sb.AppendLine($"Sheet: {Clean(request.SheetName, 100)}");
        sb.AppendLine($"Tổng số dòng: {request.TotalRows}");
        sb.AppendLine("Các cột:");

        foreach (var c in request.Columns)
        {
            sb.Append(
                $"- {Clean(c.Name, 100)} | kiểu: {Clean(c.Type, 10)}" +
                $" | có dữ liệu: {c.Count} | trống: {c.Missing}" +
                $" | giá trị khác nhau: {c.Unique}");

            if (c.Type == "Number")
            {
                sb.Append(
                    $" | tổng: {Num(c.Sum)} | trung bình: {Num(c.Mean)}" +
                    $" | min: {Num(c.Min)} | max: {Num(c.Max)}" +
                    $" | trung vị: {Num(c.Median)} | độ lệch chuẩn: {Num(c.StdDev)}");
            }
            else if (c.Type == "Date")
            {
                sb.Append(
                    $" | từ: {Clean(c.MinText ?? "", 30)}" +
                    $" | đến: {Clean(c.MaxText ?? "", 30)}");
            }
            else if (c.TopValues is { Count: > 0 })
            {
                sb.Append(" | phổ biến: " + string.Join("; ",
                    c.TopValues.Take(8).Select(v =>
                        $"{Clean(v.Value, 80)} ({v.Count})")));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // =========================================================
    // POST api/Excel/export-stats  -  Xuất bảng thống kê ra Excel
    // =========================================================

    [HttpPost("export-stats")]
    public IActionResult ExportStats(ExcelStatsRequest request)
    {
        if (request.Columns == null ||
            request.Columns.Count == 0 ||
            request.Columns.Count > MaxColumns)
        {
            return BadRequest(new { message = "Dữ liệu thống kê không hợp lệ." });
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Thống kê");

        var headers = new[]
        {
            "Cột", "Kiểu", "Có dữ liệu", "Trống", "Giá trị khác nhau",
            "Tổng", "Trung bình", "Nhỏ nhất", "Lớn nhất", "Trung vị",
            "Độ lệch chuẩn", "Từ ngày", "Đến ngày", "Giá trị phổ biến"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.SetValue(headers[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;

        foreach (var c in request.Columns)
        {
            sheet.Cell(row, 1).SetValue(Truncate(c.Name, 200));
            sheet.Cell(row, 2).SetValue(c.Type switch
            {
                "Number" => "Số",
                "Date" => "Ngày",
                _ => "Văn bản"
            });
            sheet.Cell(row, 3).SetValue(c.Count);
            sheet.Cell(row, 4).SetValue(c.Missing);
            sheet.Cell(row, 5).SetValue(c.Unique);

            SetNumber(sheet, row, 6, c.Sum);
            SetNumber(sheet, row, 7, c.Mean);
            SetNumber(sheet, row, 8, c.Min);
            SetNumber(sheet, row, 9, c.Max);
            SetNumber(sheet, row, 10, c.Median);
            SetNumber(sheet, row, 11, c.StdDev);

            sheet.Cell(row, 12).SetValue(c.MinText ?? string.Empty);
            sheet.Cell(row, 13).SetValue(c.MaxText ?? string.Empty);

            sheet.Cell(row, 14).SetValue(
                c.TopValues == null
                    ? string.Empty
                    : string.Join("; ", c.TopValues.Take(8).Select(v =>
                        $"{Truncate(v.Value, 80)} ({v.Count})")));

            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ThongKe_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    // =========================================================
    // HÀM HỖ TRỢ CHUNG
    // =========================================================

    private static void SetNumber(IXLWorksheet sheet, int row, int col, double? value)
    {
        if (value.HasValue)
        {
            sheet.Cell(row, col).SetValue(value.Value);
        }
    }

    private static string Num(double? value) =>
        value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";

    private static string Truncate(string? value, int max)
    {
        value ??= string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    // Bỏ xuống dòng và cắt ngắn để giá trị không phá cấu trúc prompt.
    private static string Clean(string? value, int max) =>
        Truncate(
            (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim(),
            max);
}

public sealed record ValueCount(string Value, int Count);

public sealed record ColumnStats(
    string Name,
    string Type,
    int Count,
    int Missing,
    int Unique,
    double? Sum,
    double? Mean,
    double? Min,
    double? Max,
    double? Median,
    double? StdDev,
    string? MinText,
    string? MaxText,
    List<ValueCount> TopValues);

public sealed class ExcelStatsRequest
{
    public string FileName { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public List<ColumnStats> Columns { get; set; } = new();
}