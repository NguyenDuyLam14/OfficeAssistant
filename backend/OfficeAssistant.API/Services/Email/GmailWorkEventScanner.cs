using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.AI;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Quét email Gmail, dùng AI (Gemini/Ollama) phát hiện công việc,
/// lưu đề xuất vào SQL Server và tạo thông báo trong ứng dụng.
/// </summary>
public sealed class GmailWorkEventScanner
{
    // Số lần gọi AI tối đa cho MỖI lượt quét của MỘT người dùng.
    // Email chưa kịp xử lý sẽ được quét ở lượt sau.
    private const int MaxAiCallsPerScan = 5;

    private readonly ApplicationDbContext _db;
    private readonly GmailMessageReader _messageReader;

    // Trước đây là OllamaService; giờ dùng interface
    // để chạy được với cả Gemini lẫn Ollama.
    private readonly IAIService _aiService;

    private readonly ILogger<GmailWorkEventScanner> _logger;

    public GmailWorkEventScanner(
        ApplicationDbContext db,
        GmailMessageReader messageReader,
        IAIService aiService,
        ILogger<GmailWorkEventScanner> logger)
    {
        _db = db;
        _messageReader = messageReader;
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Quét các email gần đây của một người dùng.
    /// Trả về số đề xuất công việc mới được lưu.
    /// </summary>
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
            query: "in:inbox newer_than:1d -category:promotions -category:social",
            cancellationToken: cancellationToken);

        var createdCount = 0;

        // Đếm số lần đã gọi AI trong lượt quét này.
        var aiCalls = 0;

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

                // BƯỚC MỚI 1: lọc sơ bộ bằng từ khóa (không tốn quota AI).
                // Email không có dấu hiệu liên quan lịch/công việc
                // thì đánh dấu đã xử lý và bỏ qua.
                if (!EmailWorkPrefilter.LooksRelevant(email))
                {
                    await MarkProcessedAsync(userId, messageId, cancellationToken);
                    continue;
                }

                // BƯỚC MỚI 2: giới hạn số lần gọi AI mỗi lượt quét.
                // KHÔNG đánh dấu đã xử lý để lượt sau xử lý tiếp.
                if (aiCalls >= MaxAiCallsPerScan)
                {
                    _logger.LogInformation(
                        "Đạt giới hạn {Max} lần gọi AI cho UserId {UserId}; " +
                        "phần email còn lại sẽ được xử lý ở lượt quét sau.",
                        MaxAiCallsPerScan,
                        userId);

                    break;
                }

                aiCalls++;

                var result = await _aiService.AnalyzeWorkEventAsync(
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
            // BƯỚC MỚI 3: hết quota Gemini thì dừng cả lượt quét.
            // Phải đặt TRƯỚC catch (Exception) bên dưới.
            // Email này chưa được đánh dấu đã xử lý nên sẽ thử lại sau.
            catch (GeminiQuotaException ex)
            {
                _logger.LogWarning(
                    "Hết quota Gemini, dừng quét cho UserId {UserId}. " +
                    "Thử lại sau {RetryAfterUtc:u}.",
                    userId,
                    ex.RetryAfterUtc);

                break;
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

                // Cố ý KHÔNG đánh dấu đã xử lý: lần quét sau sẽ thử lại.
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

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

        return null;
    }
}