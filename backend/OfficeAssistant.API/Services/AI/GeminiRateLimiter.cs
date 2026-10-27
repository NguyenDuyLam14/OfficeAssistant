namespace OfficeAssistant.API.Services.AI;

/// <summary>
/// Ném ra khi đã hết quota Gemini (do Google trả 429 hoặc do giới hạn nội bộ).
/// Caller nên dừng gọi AI và thử lại sau RetryAfterUtc.
/// </summary>
public sealed class GeminiQuotaException : Exception
{
    public DateTime RetryAfterUtc { get; }

    public GeminiQuotaException(string message, DateTime retryAfterUtc)
        : base(message)
    {
        RetryAfterUtc = retryAfterUtc;
    }
}

/// <summary>
/// Bộ giới hạn tốc độ DÙNG CHUNG (singleton) cho mọi lời gọi Gemini
/// (background scanner + người dùng bấm soạn văn bản).
///
/// - Chỉ cho 1 request chạy tại một thời điểm.
/// - Giữ khoảng cách tối thiểu giữa 2 request (RequestsPerMinute).
/// - Giới hạn số request mỗi ngày (DailyRequestLimit) thấp hơn quota thật.
/// - Khi Google trả 429, "đóng cầu dao" để các lời gọi sau không bắn tiếp.
/// </summary>
public sealed class GeminiRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();

    private readonly TimeSpan _minInterval;
    private readonly int _dailyLimit;

    private DateTime _lastRequestUtc = DateTime.MinValue;
    private DateTime _blockedUntilUtc = DateTime.MinValue;
    private DateOnly _quotaDay;
    private int _usedToday;

    public GeminiRateLimiter(IConfiguration configuration)
    {
        // Mặc định 6 request/phút: thấp hơn nhiều so với giới hạn free tier.
        var rpm = Math.Max(1, configuration.GetValue("Gemini:RequestsPerMinute", 6));

        _minInterval = TimeSpan.FromSeconds(60.0 / rpm);

        _dailyLimit = Math.Max(
            1, configuration.GetValue("Gemini:DailyRequestLimit", 100));

        _quotaDay = CurrentQuotaDay();
    }

    // Quota ngày của Google reset lúc nửa đêm giờ Pacific (~UTC-8).
    // Dùng UTC-8 cố định là cách tính hơi bảo thủ, đủ an toàn.
    private static DateOnly CurrentQuotaDay() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-8));

    private DateTime NextQuotaResetUtc() =>
        _quotaDay.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(8);

    /// <summary>
    /// Chờ đến lượt gọi Gemini. Ném GeminiQuotaException nếu đang bị khóa
    /// hoặc đã dùng hết hạn mức nội bộ trong ngày.
    /// </summary>
    public async Task WaitForTurnAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            lock (_sync)
            {
                if (now < _blockedUntilUtc)
                {
                    throw new GeminiQuotaException(
                        "Gemini đang tạm khóa do vượt giới hạn.",
                        _blockedUntilUtc);
                }

                var today = CurrentQuotaDay();

                if (today != _quotaDay)
                {
                    _quotaDay = today;
                    _usedToday = 0;
                }

                if (_usedToday >= _dailyLimit)
                {
                    throw new GeminiQuotaException(
                        $"Đã dùng hết {_dailyLimit} request Gemini trong ngày.",
                        NextQuotaResetUtc());
                }

                _usedToday++;
            }

            var wait = _lastRequestUtc + _minInterval - now;

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Khóa mọi lời gọi trong khoảng thời gian chỉ định.</summary>
    public void Block(TimeSpan duration)
    {
        lock (_sync)
        {
            var until = DateTime.UtcNow + duration;

            if (until > _blockedUntilUtc)
            {
                _blockedUntilUtc = until;
            }
        }
    }

    /// <summary>Khóa đến lúc quota ngày của Google reset.</summary>
    public DateTime BlockUntilNextQuotaDay()
    {
        lock (_sync)
        {
            var until = NextQuotaResetUtc();

            if (until > _blockedUntilUtc)
            {
                _blockedUntilUtc = until;
            }

            return _blockedUntilUtc;
        }
    }
}
