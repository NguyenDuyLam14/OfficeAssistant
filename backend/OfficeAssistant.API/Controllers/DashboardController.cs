using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.DTOs;

using System.Security.Claims;

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
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var totalDocuments = await _context.Documents
            .CountAsync(d => d.UserId == userId);

        // Công việc = sự kiện đã xác nhận của user.
        var totalTasks = await _context.DetectedWorkEvents
            .CountAsync(e => e.UserId == userId && e.Status == "Confirmed");

        var weekLater = DateTime.UtcNow.AddDays(7);

        var upcomingTasks = await _context.DetectedWorkEvents
            .CountAsync(e =>
                e.UserId == userId &&
                e.Status == "Confirmed" &&
                e.DueAtUtc != null &&
                e.DueAtUtc <= weekLater);

        return Ok(new DashboardStatsDto
        {
            TotalDocuments = totalDocuments,
            TotalEmails = 0,
            TotalTasks = totalTasks,
            UpcomingTasks = upcomingTasks
        });
    }
}