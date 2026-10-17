
using Microsoft.AspNetCore.DataProtection;

namespace OfficeAssistant.API.Services.Email;

/// <summary>
/// Mã hóa và giải mã token Gmail trước khi lưu vào cơ sở dữ liệu.
/// </summary>
public sealed class GmailTokenProtector
{
    private readonly IDataProtector _protector;

    public GmailTokenProtector(IDataProtectionProvider provider)
    {
        // Tách mục đích bảo vệ token Gmail khỏi các dữ liệu khác.
        _protector = provider.CreateProtector(
            "OfficeAssistant.GmailTokens.v1");
    }

    /// <summary>
    /// Mã hóa token trước khi lưu vào SQL Server.
    /// </summary>
    public string Protect(string token)
    {
        return _protector.Protect(token);
    }

    /// <summary>
    /// Giải mã token khi backend cần gọi Gmail API.
    /// </summary>
    public string Unprotect(string encryptedToken)
    {
        return _protector.Unprotect(encryptedToken);
    }
}
