namespace OfficeAssistant.API.DTOs;

public class DashboardStatsDto
{
    // Tổng số văn bản trong hệ thống.
    public int TotalDocuments { get; set; }

    // Tổng số email.
    // Hiện tại bảng Emails chưa được tạo,
    // nên bước đầu sẽ trả về 0.
    public int TotalEmails { get; set; }

    // Tổng số công việc.
    // Hiện tại bảng Tasks chưa được tạo,
    // nên bước đầu sẽ trả về 0.
    public int TotalTasks { get; set; }

    // Số công việc sắp đến hạn.
    // Sẽ được sử dụng khi bảng Tasks được xây dựng.
    public int UpcomingTasks { get; set; }
}