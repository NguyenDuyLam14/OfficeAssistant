
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

    public GmailWorkEventScanner(
        ApplicationDbContext db,
        GmailMessageReader messageReader,
        OllamaService ollamaService)
    {
        _db = db;
        _messageReader = messageReader;
        _ollamaService = ollamaService;
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

        // Chỉ xử lý email gần đây để giới hạn số lần gọi Ollama.
        var messageIds = await _messageReader.GetMessageIdsAsync(
            userId,
            maxResults: 20,
            query: "newer_than:1d",
            cancellationToken: cancellationToken);

        var createdCount = 0;

        foreach (var messageId in messageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Nếu email đã được xử lý thì bỏ qua để tránh tạo trùng.
            var alreadyProcessed = await _db.DetectedWorkEvents
                .AnyAsync(
                    x => x.UserId == userId
                         && x.GmailMessageId == messageId,
                    cancellationToken);

            if (alreadyProcessed)
            {
                continue;
            }

            try
            {
                // Lấy nội dung email đầy đủ từ Gmail.
                using var emailDocument =
                    await _messageReader.GetFullMessageAsync(
                        userId,
                        messageId,
                        cancellationToken);

                // Chuyển MIME của Gmail thành tiêu đề, người gửi,
                // ngày gửi và nội dung văn bản.
                var extractor = new GmailEmailContentExtractor();
                var email = extractor.Extract(emailDocument);

                // Không gửi email rỗng đến Ollama.
                if (string.IsNullOrWhiteSpace(email.Subject)
                    && string.IsNullOrWhiteSpace(email.Body))
                {
                    continue;
                }

                // Dùng chính OllamaService hiện có của dự án.
                var result = await _ollamaService.AnalyzeWorkEventAsync(
                    email.Subject ?? string.Empty,
                    email.Sender ?? string.Empty,
                    email.Date ?? string.Empty,
                    email.Body ?? string.Empty,
                    cancellationToken);

                // Email không chứa sự kiện phù hợp.
                if (result is null)
                {
                    continue;
                }

                // Chuẩn hóa thời gian về UTC.
                var startAtUtc = ParseUtc(result.StartAt);
                var dueAtUtc = ParseUtc(result.DueAt);

                var detectedEvent = new DetectedWorkEvent
                {
                    UserId = userId,
                    GmailMessageId = messageId,
                    EventType = result.EventType,
                    Title = result.Title,
                    Description = string.IsNullOrWhiteSpace(
                        result.Description)
                        ? null
                        : result.Description,
                    StartAtUtc = startAtUtc,
                    DueAtUtc = dueAtUtc,
                    TimeZoneId = "Asia/Ho_Chi_Minh",
                    Status = "PendingReview",
                    DetectedAtUtc = DateTime.UtcNow
                };

                _db.DetectedWorkEvents.Add(detectedEvent);

                // Lưu sự kiện trước để lấy khóa chính.
                await _db.SaveChangesAsync(cancellationToken);

                // Tạo thông báo liên kết với sự kiện vừa phát hiện.
                var notification = new AppNotification
                {
                    UserId = userId,
                    Title = "Phát hiện công việc mới",
                    Message = $"AI phát hiện đề xuất: {detectedEvent.Title}. "
                              + "Vui lòng kiểm tra trước khi xác nhận.",
                    NotificationType = "DetectedWorkEvent",
                    DetectedWorkEventId =
                        detectedEvent.DetectedWorkEventId,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.AppNotifications.Add(notification);
                await _db.SaveChangesAsync(cancellationToken);

                createdCount++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Một email lỗi không làm dừng toàn bộ lượt quét.
                // Chưa ghi log ở bước này; sẽ bổ sung logging
                // khi tích hợp background worker.
                continue;
            }
        }

        return createdCount;
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
