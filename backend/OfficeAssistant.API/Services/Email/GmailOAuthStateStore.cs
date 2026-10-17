
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Lưu state tạm thời để chống giả mạo OAuth callback.
/// Mỗi state chỉ được sử dụng một lần và hết hạn sau 10 phút.
/// </summary>
public sealed class GmailOAuthStateStore
{
    private sealed record StateEntry(int UserId, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, StateEntry> _states = new();

    public string Create(int userId)
    {
        // Dọn các state đã hết hạn.
        var now = DateTime.UtcNow;

        foreach (var item in _states)
        {
            if (item.Value.ExpiresAtUtc <= now)
            {
                _states.TryRemove(item.Key, out _);
            }
        }

        // Tạo state ngẫu nhiên bằng bộ sinh số an toàn mật mã.
        var state = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        _states[state] = new StateEntry(
            userId,
            now.AddMinutes(10));

        return state;
    }

    /// <summary>
    /// Xác minh và tiêu thụ state. State không thể dùng lại lần thứ hai.
    /// </summary>
    public bool TryConsume(string state, out int userId)
    {
        userId = 0;

        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        if (!_states.TryRemove(state, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return false;
        }

        userId = entry.UserId;
        return true;
    }
}
