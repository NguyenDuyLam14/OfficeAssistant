using System.Net;
using System.Text;

namespace OfficeAssistant.API.Services.Email;

// Bổ sung hàm POST cho GmailMessageReader (dùng để gửi thư).
// Yêu cầu: khai báo GmailMessageReader là "public sealed partial class".
public sealed partial class GmailMessageReader
{
    /// <summary>
    /// Gửi POST JSON đến Gmail API; thử refresh token một lần khi gặp HTTP 401.
    /// Caller chịu trách nhiệm dispose HttpResponseMessage.
    /// </summary>
    public async Task<HttpResponseMessage> SendGmailPostAsync(
        int userId,
        string url,
        string jsonBody,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();

        var accessToken = await GetValidAccessTokenAsync(
            userId,
            cancellationToken: cancellationToken);

        var response = await PostWithTokenAsync(
            client, url, jsonBody, accessToken, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();

        accessToken = await GetValidAccessTokenAsync(
            userId,
            forceRefresh: true,
            cancellationToken: cancellationToken);

        return await PostWithTokenAsync(
            client, url, jsonBody, accessToken, cancellationToken);
    }

    private static async Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string url,
        string jsonBody,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                jsonBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

        return await client.SendAsync(request, cancellationToken);
    }
}
