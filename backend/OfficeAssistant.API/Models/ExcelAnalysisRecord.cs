namespace OfficeAssistant.API.Models;

/// <summary>
/// Lịch sử các lần phân tích file Excel của người dùng.
/// Chỉ lưu thông tin tổng quan, không lưu nội dung file.
/// </summary>
public class ExcelAnalysisRecord
{
    public int ExcelAnalysisRecordId { get; set; }

    public int UserId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SheetName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int ColumnCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}