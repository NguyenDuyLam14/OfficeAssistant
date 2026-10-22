
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.AI;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Quét email Gmail, dùng Ollama phát hiện công việc,
/// lưu đề xuất vào SQL Server và tạo thông báo trong ứng dụng.
/// </summary>
public sealed class GmailWorkEventScanner
{
    private readonly ApplicationDbContext _db;
    private readonly GmailMessageReader _messageReader;
    private readonly OllamaService _ollamaService;

    /// <summary>
    /// Quét các email gần đây của một người dùng.
    /// Trả về số đề xuất công việc mới được lưu.
    /// </summary>
    private readonly ILogger<GmailWorkEventScanner> _logger;

    public GmailWorkEventScanner(
        ApplicationDbContext db,
        GmailMessageReader messageReader,
        OllamaService ollamaService,
        ILogger<GmailWorkEventScanner> logger)
    {
        _db = db;
        _messageReader = messageReader;
        _ollamaService = ollamaService;
        _logger = logger;
    }

    public async Task<int> ScanUserEmailsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var messageIds = await _messageReader.GetMessageIdsAsync(
            userId,
            maxResults: 20,
            query: "newer_than:1d",
            cancellationToken: cancellationToken);

        var createdCount = 0;

        foreach (var messageId in messageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Đã xử lý rồi (có sự kiện hoặc không) thì bỏ qua.
            var alreadyProcessed =
                await _db.ProcessedGmailMessages.AnyAsync(
                    x => x.UserId == userId && x.GmailMessageId == messageId,
                    cancellationToken)
                || await _db.DetectedWorkEvents.AnyAsync(
                    x => x.UserId == userId && x.GmailMessageId == messageId,
                    cancellationToken);

            if (alreadyProcessed)
            {
                continue;
            }

            try
            {
                using var emailDocument =
                    await _messageReader.GetFullMessageAsync(
                        userId, messageId, cancellationToken);

                var email = new GmailEmailContentExtractor()
                    .Extract(emailDocument);

                // Email rỗng: đánh dấu đã xử lý, không gọi AI.
                if (string.IsNullOrWhiteSpace(email.Subject)
                    && string.IsNullOrWhiteSpace(email.Body))
                {
                    await MarkProcessedAsync(userId, messageId, cancellationToken);
                    continue;
                }

                var result = await _ollamaService.AnalyzeWorkEventAsync(
                    email.Subject ?? string.Empty,
                    email.Sender ?? string.Empty,
                    email.Date ?? string.Empty,
                    email.Body ?? string.Empty,
                    cancellationToken);

                // AI xác định không có sự kiện: nhớ lại để lần sau khỏi hỏi AI.
                if (result is null)
                {
                    await MarkProcessedAsync(userId, messageId, cancellationToken);
                    continue;
                }

                var detectedEvent = new DetectedWorkEvent
                {
                    UserId = userId,
                    GmailMessageId = messageId,
                    EventType = result.EventType,
                    Title = result.Title,
                    Description = string.IsNullOrWhiteSpace(result.Description)
                        ? null
                        : result.Description,
                    StartAtUtc = ParseUtc(result.StartAt),
                    DueAtUtc = ParseUtc(result.DueAt),
                    TimeZoneId = "Asia/Ho_Chi_Minh",
                    Status = "PendingReview",
                    DetectedAtUtc = DateTime.UtcNow
                };

                _db.DetectedWorkEvents.Add(detectedEvent);
                await _db.SaveChangesAsync(cancellationToken);

                _db.AppNotifications.Add(new AppNotification
                {
                    UserId = userId,
                    Title = "Phát hiện công việc mới",
                    Message = $"AI phát hiện đề xuất: {detectedEvent.Title}. "
                              + "Vui lòng kiểm tra trước khi xác nhận.",
                    NotificationType = "DetectedWorkEvent",
                    DetectedWorkEventId = detectedEvent.DetectedWorkEventId,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                });

                _db.ProcessedGmailMessages.Add(new ProcessedGmailMessage
                {
                    UserId = userId,
                    GmailMessageId = messageId
                });

                await _db.SaveChangesAsync(cancellationToken);

                createdCount++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không thể xử lý email {MessageId} của UserId {UserId}.",
                    messageId,
                    userId);

                // Xóa entity bị lỗi để không làm hỏng các lần SaveChanges sau.
                _db.ChangeTracker.Clear();

                // Cố ý KHÔNG đánh dấu đã xử lý: lần quét sau sẽ thử lại
                // (ví dụ khi Ollama tạm thời chưa chạy).
                continue;
            }
        }

        return createdCount;
    }

    private async Task MarkProcessedAsync(
        int userId,
        string messageId,
        CancellationToken cancellationToken)
    {
        _db.ProcessedGmailMessages.Add(new ProcessedGmailMessage
        {
            UserId = userId,
            GmailMessageId = messageId
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Chuyển thời gian AI trích xuất thành UTC để lưu vào cơ sở dữ liệu.
    /// Nếu thời gian không có múi giờ, mặc định đó là giờ Việt Nam (UTC+7).
    /// </summary>
    private static DateTime? ParseUtc(string? value)
    {
        // Không có dữ liệu thời gian thì không thể chuyển đổi.
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        // Kiểm tra chuỗi có múi giờ rõ ràng hay không.
        bool hasExplicitTimeZone =
            value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || (
                value.Length >= 6
                && (value[^6] == '+' || value[^6] == '-')
                && value[^3] == ':'
                && char.IsDigit(value[^5])
                && char.IsDigit(value[^4])
                && char.IsDigit(value[^2])
                && char.IsDigit(value[^1])
            );

        // Trường hợp thời gian đã có múi giờ, ví dụ:
        // 2026-10-25T17:00:00+07:00
        // 2026-10-25T10:00:00Z
        if (hasExplicitTimeZone)
        {
            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset dateTimeWithOffset))
            {
                return dateTimeWithOffset.UtcDateTime;
            }

            return null;
        }

        // Trường hợp không có múi giờ, ví dụ:
        // 2026-10-25T17:00:00
        // Mặc định giờ trên là giờ Việt Nam (UTC+7).
        if (DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime localDateTime))
        {
            var vietnamTime = new DateTimeOffset(
                DateTime.SpecifyKind(
                    localDateTime,
                    DateTimeKind.Unspecified),
                TimeSpan.FromHours(7));

            return vietnamTime.UtcDateTime;
        }

        // Chuỗi không hợp lệ thì bỏ qua thời gian này.
        return null;
    }
}
