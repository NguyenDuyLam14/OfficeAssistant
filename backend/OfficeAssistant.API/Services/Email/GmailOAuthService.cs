using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Xử lý OAuth 2.0 và các yêu cầu xác thực với Google.
/// </summary>
public sealed class GmailOAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailOAuthService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    private string ClientId =>
        _configuration["GoogleOAuth:ClientId"]
        ?? throw new InvalidOperationException(
            "Thiếu GoogleOAuth:ClientId trong User Secrets.");

    private string ClientSecret =>
        _configuration["GoogleOAuth:ClientSecret"]
        ?? throw new InvalidOperationException(
            "Thiếu GoogleOAuth:ClientSecret trong User Secrets.");

    private string RedirectUri =>
        _configuration["GoogleOAuth:RedirectUri"]
        ?? throw new InvalidOperationException(
            "Thiếu GoogleOAuth:RedirectUri trong User Secrets.");

    /// <summary>
    /// Tạo URL để người dùng đăng nhập và cấp quyền Gmail.
    /// </summary>
    public string CreateAuthorizationUrl(string state)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] =
                "openid email https://www.googleapis.com/auth/gmail.readonly " +
                "https://www.googleapis.com/auth/gmail.send",
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",

            // Yêu cầu Google hiển thị màn hình chọn tài khoản
            // và màn hình cấp quyền khi cần thiết.
            ["prompt"] = "select_account consent",

            ["state"] = state
        };

        return QueryHelpers.AddQueryString(
            "https://accounts.google.com/o/oauth2/v2/auth",
            parameters);
    }

    /// <summary>
    /// Đổi authorization code lấy access token và refresh token.
    /// </summary>
    public async Task<GoogleOAuthTokenResponse> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();

        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["redirect_uri"] = RedirectUri,
                    ["grant_type"] = "authorization_code"
                }),
            cancellationToken);

        return await ReadTokenResponseAsync(response, cancellationToken);
    }

    /// <summary>
    /// Làm mới access token khi token cũ hết hạn hoặc sắp hết hạn.
    /// Refresh token được truyền từ backend sau khi giải mã.
    /// Không gửi refresh token xuống frontend.
    /// </summary>
    public async Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException(
                "Không có refresh token để làm mới quyền truy cập Gmail.");
        }

        var client = _httpClientFactory.CreateClient();

        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                }),
            cancellationToken);

        return await ReadTokenResponseAsync(response, cancellationToken);
    }

    /// <summary>
    /// Đọc và kiểm tra phản hồi token của Google.
    /// Không đưa token hoặc nội dung phản hồi chứa thông tin bí mật
    /// vào thông báo lỗi.
    /// </summary>
    private static async Task<GoogleOAuthTokenResponse>
        ReadTokenResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google OAuth thất bại: HTTP {(int)response.StatusCode}.");
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            var token =
                await JsonSerializer.DeserializeAsync<GoogleOAuthTokenResponse>(
                    stream,
                    cancellationToken: cancellationToken);

            if (token == null ||
                string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException(
                    "Google không trả về access token hợp lệ.");
            }

            return token;
        }
    }

    /// <summary>
    /// Lấy địa chỉ email Google từ access token.
    /// </summary>
    public async Task<string> GetGoogleEmailAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Không lấy được thông tin Google: HTTP {(int)response.StatusCode}.");
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("email", out var email))
        {
            throw new InvalidOperationException(
                "Google không trả về địa chỉ email.");
        }

        return email.GetString()
            ?? throw new InvalidOperationException(
                "Địa chỉ email Google không hợp lệ.");
    }
}