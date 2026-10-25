using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Models;
using OfficeAssistant.API.Services.Email;

namespace OfficeAssistant.API.Controllers;

/// <summary>
/// API kết nối tài khoản Gmail của người dùng OfficeAssistant.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class EmailsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly GmailOAuthService _oauthService;
    private readonly GmailOAuthStateStore _stateStore;
    private readonly GmailTokenProtector _tokenProtector;
    private readonly IConfiguration _configuration;

    public EmailsController(
        ApplicationDbContext context,
        GmailOAuthService oauthService,
        GmailOAuthStateStore stateStore,
        GmailTokenProtector tokenProtector,
        IConfiguration configuration)
    {
        _context = context;
        _oauthService = oauthService;
        _stateStore = stateStore;
        _tokenProtector = tokenProtector;
        _configuration = configuration;
    }

    /// <summary>
    /// Lấy UserId từ JWT đã được xác thực.
    /// Không nhận UserId từ dữ liệu frontend gửi lên.
    /// </summary>
    private bool TryGetCurrentUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out userId) && userId > 0;
    }

    /// <summary>
    /// Bắt đầu luồng kết nối Gmail.
    /// GET /api/Emails/connect
    /// </summary>
    [HttpGet("connect")]
    public IActionResult Connect()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        // Tạo state ngẫu nhiên gắn với người dùng hiện tại.
        var state = _stateStore.Create(userId);

        // Frontend sẽ chuyển trình duyệt tới URL Google trả về.
        var authorizationUrl =
            _oauthService.CreateAuthorizationUrl(state);

        return Ok(new
        {
            authorizationUrl
        });
    }

    /// <summary>
    /// Google chuyển về endpoint này sau khi người dùng cấp quyền.
    /// GET /api/Emails/oauth2callback
    /// </summary>
    [AllowAnonymous]
    [HttpGet("oauth2callback")]
    public async Task<IActionResult> OAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        // Kiểm tra state trước khi xử lý kết quả OAuth.
        if (!_stateStore.TryConsume(state ?? string.Empty, out var userId))
        {
            return BadRequest(new
            {
                message = "OAuth state không hợp lệ, đã hết hạn hoặc đã được sử dụng."
            });
        }

        // Người dùng có thể từ chối cấp quyền tại Google.
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new
            {
                message = "Bạn chưa cấp quyền kết nối Gmail."
            });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new
            {
                message = "Google không trả về authorization code."
            });
        }

        // Chỉ liên kết với tài khoản OfficeAssistant đã tạo state.
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.IsActive,
                cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Tài khoản OfficeAssistant không tồn tại hoặc đã bị vô hiệu hóa."
            });
        }

        try
        {
            // Đổi code lấy token từ Google.
            var token = await _oauthService.ExchangeCodeAsync(
                code, cancellationToken);

            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return BadRequest(new
                {
                    message = "Google không trả về access token hợp lệ."
                });
            }

            // Lấy địa chỉ Gmail từ tài khoản Google vừa cấp quyền.
            var gmailAddress = await _oauthService.GetGoogleEmailAsync(
                token.AccessToken, cancellationToken);

            gmailAddress = gmailAddress.Trim().ToLowerInvariant();

            // Một địa chỉ Gmail không được liên kết với hai tài khoản.
            var gmailUsedByAnotherUser = await _context.GmailConnections
                .AnyAsync(
                    x => x.GmailAddress == gmailAddress &&
                        x.UserId != userId,
                    cancellationToken);

            if (gmailUsedByAnotherUser)
            {
                return Conflict(new
                {
                    message = "Địa chỉ Gmail này đã được liên kết với tài khoản OfficeAssistant khác."
                });
            }

            var connection = await _context.GmailConnections
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            // Mã hóa token trước khi lưu SQL Server.
            var encryptedAccessToken =
                _tokenProtector.Protect(token.AccessToken);

            // Google có thể không trả refresh token ở những lần cấp quyền sau.
            // Nếu vậy, giữ refresh token cũ để không làm mất khả năng làm mới.
            string? encryptedRefreshToken = null;

            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                encryptedRefreshToken =
                    _tokenProtector.Protect(token.RefreshToken);
            }

            var now = DateTime.UtcNow;

            if (connection == null)
            {
                connection = new GmailConnection
                {
                    UserId = userId,
                    GmailAddress = gmailAddress,
                    AccessToken = encryptedAccessToken,
                    RefreshToken = encryptedRefreshToken,
                    TokenExpiresAtUtc =
                        now.AddSeconds(Math.Max(0, token.ExpiresIn)),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _context.GmailConnections.Add(connection);
            }
            else
            {
                connection.GmailAddress = gmailAddress;
                connection.AccessToken = encryptedAccessToken;

                if (encryptedRefreshToken != null)
                {
                    connection.RefreshToken = encryptedRefreshToken;
                }

                connection.TokenExpiresAtUtc =
                    now.AddSeconds(Math.Max(0, token.ExpiresIn));

                connection.UpdatedAtUtc = now;
            }

            // Lưu kết nối Gmail vào SQL Server.
            await _context.SaveChangesAsync(cancellationToken);

            var frontendUrl =
                _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";

            return Redirect($"{frontendUrl}/emails?gmailConnected=true");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Không đưa token, authorization code hoặc thông tin bí mật
            // vào phản hồi lỗi.
            return StatusCode(500, new
            {
                message = "Không thể hoàn tất kết nối Gmail. Vui lòng thử lại."
            });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái kết nối Gmail của người dùng hiện tại.
    /// GET /api/Emails/status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được người dùng đang đăng nhập."
            });
        }

        var connection = await _context.GmailConnections
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.GmailAddress,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.TokenExpiresAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (connection == null)
        {
            return Ok(new
            {
                connected = false,
                gmailAddress = (string?)null
            });
        }

        return Ok(new
        {
            connected = true,
            gmailAddress = connection.GmailAddress,
            connection.CreatedAtUtc,
            connection.UpdatedAtUtc,
            connection.TokenExpiresAtUtc
        });
    }
}
