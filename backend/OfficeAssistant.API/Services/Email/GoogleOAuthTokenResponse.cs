
using System.Text.Json.Serialization;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Dữ liệu token Google trả về sau khi đổi authorization code.
/// </summary>
public sealed class GoogleOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }
}
