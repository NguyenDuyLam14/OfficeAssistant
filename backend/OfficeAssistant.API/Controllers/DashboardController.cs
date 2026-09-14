using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;

namespace OfficeAssistant.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        // Đếm tổng số văn bản trong cơ sở dữ liệu.
        var totalDocuments = await _context.Documents
            .CountAsync();

        // Email và công việc sẽ được kết nối
        // sau khi xây dựng các bảng tương ứng.
        var totalEmails = 0;
        var totalTasks = 0;
        var upcomingTasks = 0;

        var result = new DashboardStatsDto
        {
            TotalDocuments = totalDocuments,
            TotalEmails = totalEmails,
            TotalTasks = totalTasks,
            UpcomingTasks = upcomingTasks
        };

        return Ok(result);
    }
}