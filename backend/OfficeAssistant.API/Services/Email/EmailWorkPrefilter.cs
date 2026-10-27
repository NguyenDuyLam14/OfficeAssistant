namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Lọc sơ bộ BẰNG CODE (không tốn quota) trước khi gửi email cho AI.
/// Chỉ những email có dấu hiệu liên quan lịch/công việc mới gọi Gemini.
/// </summary>
public static class EmailWorkPrefilter
{
    private static readonly string[] Keywords =
    {
        // Tiếng Việt
        "họp", "cuộc họp", "lịch hẹn", "hẹn", "hạn chót", "hạn nộp",
        "hạn hoàn thành", "trước ngày", "nộp", "deadline", "gấp",
        "yêu cầu", "triển khai", "phỏng vấn", "tập huấn", "hội nghị",

        // Tiếng Anh
        "meeting", "appointment", "invitation", "invite", "due",
        "schedule", "reminder", "interview", "call", "calendar"
    };

    public static bool LooksRelevant(GmailEmailContent email)
    {
        var text = ($"{email.Subject} {email.Body}").ToLowerInvariant();

        return Keywords.Any(keyword => text.Contains(keyword));
    }
}
