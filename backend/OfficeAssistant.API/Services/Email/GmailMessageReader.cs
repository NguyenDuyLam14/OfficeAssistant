
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Data;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Đọc email từ Gmail cho một người dùng cụ thể.
/// Dịch vụ dùng chung cho API và background worker.
/// Không nhận UserId từ dữ liệu email hoặc query string phía client.
/// </summary>
public sealed class GmailMessageReader
{
    private readonly ApplicationDbContext _db;
    private readonly GmailOAuthService _oauthService;
    private readonly GmailTokenProtector _tokenProtector;
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailMessageReader(
        ApplicationDbContext db,
        GmailOAuthService oauthService,
        GmailTokenProtector tokenProtector,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _oauthService = oauthService;
        _tokenProtector = tokenProtector;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Lấy access token còn hiệu lực; tự làm mới nếu cần.
    /// </summary>
    public async Task<string> GetValidAccessTokenAsync(
        int userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await _db.GmailConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (connection == null)
        {
            throw new InvalidOperationException(
                "Người dùng chưa kết nối Gmail.");
        }

        var tokenStillValid =
            connection.TokenExpiresAtUtc.HasValue &&
            connection.TokenExpiresAtUtc.Value >
                DateTime.UtcNow.AddMinutes(5);

        if (!forceRefresh && tokenStillValid)
        {
            try
            {
                return _tokenProtector.Unprotect(connection.AccessToken);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Không thể giải mã access token Gmail.", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            throw new InvalidOperationException(
                "Kết nối Gmail không có refresh token.");
        }

        string refreshToken;

        try
        {
            refreshToken =
                _tokenProtector.Unprotect(connection.RefreshToken);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Không thể giải mã refresh token Gmail.", ex);
        }

        var refreshed = await _oauthService.RefreshAccessTokenAsync(
            refreshToken,
            cancellationToken);

        connection.AccessToken =
            _tokenProtector.Protect(refreshed.AccessToken);

        connection.TokenExpiresAtUtc =
            DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);

        connection.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return refreshed.AccessToken;
    }

    /// <summary>
    /// Gửi GET đến Gmail API và thử refresh token một lần khi gặp HTTP 401.
    /// Caller chịu trách nhiệm dispose HttpResponseMessage.
    /// </summary>
    public async Task<HttpResponseMessage> SendGmailRequestAsync(
        int userId,
        string url,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();

        var accessToken = await GetValidAccessTokenAsync(
            userId,
            cancellationToken: cancellationToken);

        var response = await SendWithTokenAsync(
            client, url, accessToken, cancellationToken);

        if (response.StatusCode !=
            System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();

        accessToken = await GetValidAccessTokenAsync(
            userId,
            forceRefresh: true,
            cancellationToken: cancellationToken);

        return await SendWithTokenAsync(
            client, url, accessToken, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client,
        string url,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Lấy ID email mới nhất theo truy vấn Gmail.
    /// Chưa đọc nội dung email ở bước này.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetMessageIdsAsync(
        int userId,
        int maxResults = 20,
        string query = "newer_than:1d",
        CancellationToken cancellationToken = default)
    {
        if (maxResults < 1 || maxResults > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResults),
                "maxResults phải nằm trong khoảng 1 đến 100.");
        }

        var parameters = new Dictionary<string, string?>
        {
            ["maxResults"] = maxResults.ToString(),
            ["q"] = query
        };

        var url = QueryHelpers.AddQueryString(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages",
            parameters);

        using var response = await SendGmailRequestAsync(
            userId, url, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        var ids = new List<string>();

        if (document.RootElement.TryGetProperty(
                "messages", out var messages) &&
            messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (message.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                {
                    var id = idElement.GetString();

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Lấy nội dung email ở định dạng full để xử lý bằng AI.
    /// </summary>
    public async Task<JsonDocument> GetFullMessageAsync(
        int userId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId) ||
            messageId.Length > 200 ||
            messageId.Any(c =>
                !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
        {
            throw new ArgumentException(
                "Mã email không hợp lệ.", nameof(messageId));
        }

        var url =
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/" +
            Uri.EscapeDataString(messageId) +
            "?format=full";

        using var response = await SendGmailRequestAsync(
            userId, url, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);
    }
}
