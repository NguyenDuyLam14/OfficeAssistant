
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Tự động quét email Gmail định kỳ trong nền.
/// Mỗi người dùng được xử lý trong một scope DI riêng.
/// </summary>
public sealed class GmailWorkEventBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GmailWorkEventBackgroundService> _logger;

    // Chu kỳ quét ban đầu: 5 phút.
    private static readonly TimeSpan ScanInterval =
        TimeSpan.FromMinutes(15);

    public GmailWorkEventBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<GmailWorkEventBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Gmail work-event background service đã khởi động.");

        // Chờ một chút để ứng dụng hoàn tất khởi động.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAllUsersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi khi quét email Gmail trong nền.");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Gmail work-event background service đã dừng.");
    }

    private async Task ScanAllUsersAsync(
        CancellationToken cancellationToken)
    {
        // Lấy ID người dùng đã lưu kết nối Gmail.
        // Chỉ lấy ID để không giữ DbContext ngoài scope.
        List<int> userIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            userIds = await db.GmailConnections
                .AsNoTracking()
                .Select(connection => connection.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Tạo scope riêng cho mỗi người dùng.
                using var scope = _scopeFactory.CreateScope();

                var scanner = scope.ServiceProvider
                    .GetRequiredService<GmailWorkEventScanner>();

                var count = await scanner.ScanUserEmailsAsync(
                    userId,
                    cancellationToken);

                if (count > 0)
                {
                    _logger.LogInformation(
                        "Đã tạo {Count} đề xuất công việc cho UserId {UserId}.",
                        count,
                        userId);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Một tài khoản lỗi không ngăn các tài khoản khác được quét.
                _logger.LogError(
                    ex,
                    "Không thể quét Gmail cho UserId {UserId}.",
                    userId);
            }
        }
    }
}
