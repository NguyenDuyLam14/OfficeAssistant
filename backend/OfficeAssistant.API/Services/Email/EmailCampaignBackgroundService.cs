using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Gửi email của các chiến dịch đang Queued/Running.
///
/// - Gửi tuần tự, có độ trễ giữa các thư (SecondsBetweenEmails).
/// - Mỗi lượt chỉ gửi tối đa SliceSize thư cho một chiến dịch,
///   rồi chuyển sang chiến dịch khác để chia đều giữa người dùng.
/// - Giới hạn số thư/24 giờ cho mỗi người dùng (DailyLimitPerUser).
/// - Trạng thái nằm trong SQL Server nên chạy tiếp được sau khi khởi động lại.
/// </summary>
public sealed class EmailCampaignBackgroundService : BackgroundService
{
    private const int SliceSize = 10;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailCampaignBackgroundService> _logger;
    private readonly TimeSpan _delayBetweenEmails;
    private readonly int _dailyLimit;

    // Người dùng đang bị tạm dừng (hết hạn mức, Gmail giới hạn tốc độ...).
    private readonly Dictionary<int, DateTime> _blockedUntil = new();

    public EmailCampaignBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailCampaignBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _delayBetweenEmails = TimeSpan.FromSeconds(Math.Max(
            1, configuration.GetValue("EmailCampaign:SecondsBetweenEmails", 4)));

        // Gmail cá nhân ~500 thư/ngày; đặt trần thấp hơn để an toàn.
        _dailyLimit = Math.Max(
            1, configuration.GetValue("EmailCampaign:DailyLimitPerUser", 400));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email campaign service đã khởi động.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý chiến dịch email.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Email campaign service đã dừng.");
    }

    private async Task ProcessAllAsync(CancellationToken cancellationToken)
    {
        List<int> campaignIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            campaignIds = await db.EmailCampaigns
                .AsNoTracking()
                .Where(x => x.Status == "Queued" || x.Status == "Running")
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => x.EmailCampaignId)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in campaignIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessCampaignSliceAsync(id, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Một chiến dịch lỗi không ngăn các chiến dịch khác.
                _logger.LogError(
                    ex, "Không thể xử lý chiến dịch {CampaignId}.", id);
            }
        }
    }

    private async Task ProcessCampaignSliceAsync(
        int campaignId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var sender = scope.ServiceProvider
            .GetRequiredService<GmailSender>();

        var campaign = await db.EmailCampaigns.FirstOrDefaultAsync(
            x => x.EmailCampaignId == campaignId, cancellationToken);

        if (campaign == null ||
            (campaign.Status != "Queued" && campaign.Status != "Running"))
        {
            return;
        }

        var userId = campaign.UserId;

        if (_blockedUntil.TryGetValue(userId, out var until) &&
            until > DateTime.UtcNow)
        {
            return;
        }

        if (campaign.Status == "Queued")
        {
            campaign.Status = "Running";
            campaign.StartedAtUtc = DateTime.UtcNow;
            campaign.Note = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        for (var i = 0; i < SliceSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Người dùng có thể đã hủy chiến dịch trong lúc đang gửi.
            await db.Entry(campaign).ReloadAsync(cancellationToken);

            if (campaign.Status != "Running")
            {
                return;
            }

            // ----- Kiểm tra hạn mức 24 giờ -----
            var since = DateTime.UtcNow.AddHours(-24);

            var sentLast24h = await db.EmailCampaignRecipients.CountAsync(
                x => x.Status == "Sent" &&
                     x.SentAtUtc >= since &&
                     x.Campaign.UserId == userId,
                cancellationToken);

            if (sentLast24h >= _dailyLimit)
            {
                campaign.Note =
                    $"Đã đạt hạn mức {_dailyLimit} thư/24 giờ. " +
                    "Chiến dịch sẽ tự tiếp tục khi còn hạn mức.";

                await db.SaveChangesAsync(cancellationToken);
                Block(userId, TimeSpan.FromMinutes(15));
                return;
            }

            var recipient = await db.EmailCampaignRecipients
                .Where(x => x.EmailCampaignId == campaignId &&
                            x.Status == "Pending")
                .OrderBy(x => x.EmailCampaignRecipientId)
                .FirstOrDefaultAsync(cancellationToken);

            // Hết người nhận: hoàn thành.
            if (recipient == null)
            {
                campaign.Status = "Completed";
                campaign.FinishedAtUtc = DateTime.UtcNow;
                campaign.Note = null;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            if (campaign.Note != null)
            {
                campaign.Note = null;
            }

            try
            {
                var values = EmailTemplateRenderer.BuildValues(
                    recipient.Email, recipient.Name, recipient.DataJson);

                var subject = EmailTemplateRenderer.Render(
                    campaign.Subject, values);

                var body = EmailTemplateRenderer.Render(
                    campaign.BodyTemplate, values);

                await sender.SendAsync(
                    userId,
                    new GmailSendRequest(
                        recipient.Email, null, subject, body, null),
                    cancellationToken);

                recipient.Status = "Sent";
                recipient.SentAtUtc = DateTime.UtcNow;
                recipient.Error = null;
            }
            catch (GmailSendException ex) when (ex.StatusCode is 401 or 403)
            {
                // Mất quyền Gmail: dừng cả chiến dịch.
                await FailCampaignAsync(db, campaign, ex.Message, cancellationToken);
                return;
            }
            catch (GmailSendException ex) when (ex.StatusCode == 429)
            {
                // Gmail giới hạn tốc độ/hạn mức: tạm dừng, thử lại sau.
                campaign.Note = ex.Message + " Sẽ tự thử lại sau.";
                await db.SaveChangesAsync(cancellationToken);
                Block(userId, TimeSpan.FromMinutes(30));
                return;
            }
            catch (InvalidOperationException ex)
            {
                // Chưa kết nối Gmail hoặc không làm mới được token.
                _logger.LogWarning(
                    ex, "Chiến dịch {CampaignId} mất kết nối Gmail.", campaignId);

                await FailCampaignAsync(
                    db,
                    campaign,
                    "Kết nối Gmail không còn hiệu lực. Hãy kết nối lại Gmail rồi tạo lại chiến dịch.",
                    cancellationToken);

                return;
            }
            catch (HttpRequestException)
            {
                // Lỗi mạng tạm thời: không đánh dấu người nhận là lỗi.
                campaign.Note = "Mất kết nối mạng tới Gmail. Sẽ tự thử lại.";
                await db.SaveChangesAsync(cancellationToken);
                Block(userId, TimeSpan.FromMinutes(2));
                return;
            }
            catch (Exception ex) when (
                ex is GmailSendException or ArgumentException)
            {
                // Lỗi riêng của người nhận này (địa chỉ bị từ chối...).
                recipient.Status = "Failed";
                recipient.Error = Truncate(ex.Message, 500);
            }

            await db.SaveChangesAsync(cancellationToken);

            await Task.Delay(_delayBetweenEmails, cancellationToken);
        }
    }

    private static async Task FailCampaignAsync(
        ApplicationDbContext db,
        EmailCampaign campaign,
        string note,
        CancellationToken cancellationToken)
    {
        campaign.Status = "Failed";
        campaign.Note = Truncate(note, 500);
        campaign.FinishedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    private void Block(int userId, TimeSpan duration) =>
        _blockedUntil[userId] = DateTime.UtcNow + duration;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
