using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Tạo thông báo nhắc việc cho các công việc đã xác nhận sắp đến hạn:
/// - Reminder24h: còn dưới 24 giờ
/// - Reminder1h: còn dưới 1 giờ
/// Mỗi mức chỉ nhắc một lần cho mỗi công việc.
/// </summary>
public sealed class WorkEventReminderBackgroundService : BackgroundService
{
    public const string Type24h = "Reminder24h";
    public const string Type1h = "Reminder1h";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkEventReminderBackgroundService> _logger;

    public WorkEventReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkEventReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder service đã khởi động.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo nhắc việc.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Reminder service đã dừng.");
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var horizon = now.AddHours(24);

        // Mốc thời gian của công việc: giờ bắt đầu, nếu không có thì hạn chót.
        var events = await db.DetectedWorkEvents
            .AsNoTracking()
            .Where(x => x.Status == "Confirmed"
                        && (x.StartAtUtc ?? x.DueAtUtc) != null
                        && (x.StartAtUtc ?? x.DueAtUtc) > now
                        && (x.StartAtUtc ?? x.DueAtUtc) <= horizon)
            .Select(x => new
            {
                x.DetectedWorkEventId,
                x.UserId,
                x.Title,
                At = x.StartAtUtc ?? x.DueAtUtc
            })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return;
        }

        var ids = events.Select(x => x.DetectedWorkEventId).ToList();

        var existing = await db.AppNotifications
            .AsNoTracking()
            .Where(n => n.DetectedWorkEventId != null
                        && ids.Contains(n.DetectedWorkEventId.Value)
                        && (n.NotificationType == Type24h
                            || n.NotificationType == Type1h))
            .Select(n => new
            {
                Id = n.DetectedWorkEventId!.Value,
                n.NotificationType
            })
            .ToListAsync(cancellationToken);

        var sent = existing
            .Select(x => (x.Id, x.NotificationType))
            .ToHashSet();

        var created = 0;

        foreach (var item in events)
        {
            var at = item.At!.Value;

            var type = at - now <= TimeSpan.FromHours(1) ? Type1h : Type24h;

            if (sent.Contains((item.DetectedWorkEventId, type)))
            {
                continue;
            }

            // Giờ Việt Nam (UTC+7) để hiển thị cho người dùng.
            var when = at.AddHours(7).ToString(
                "HH:mm 'ngày' dd/MM/yyyy", CultureInfo.InvariantCulture);

            var title = item.Title.Length > 300
                ? item.Title[..300]
                : item.Title;

            db.AppNotifications.Add(new AppNotification
            {
                UserId = item.UserId,
                Title = type == Type1h
                    ? "Nhắc việc: còn dưới 1 giờ"
                    : "Nhắc việc: còn dưới 24 giờ",
                Message = $"{title} - {when}.",
                NotificationType = type,
                DetectedWorkEventId = item.DetectedWorkEventId,
                IsRead = false,
                CreatedAtUtc = now
            });

            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Đã tạo {Count} nhắc việc.", created);
        }
    }
}