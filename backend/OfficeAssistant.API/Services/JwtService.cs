using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Services;

/// <summary>
/// Service chịu trách nhiệm tạo JWT token
/// cho người dùng sau khi đăng nhập thành công.
/// </summary>
public class JwtService
{
    private readonly JwtSettings _jwtSettings;

    /// <summary>
    /// Nhận cấu hình JWT từ Dependency Injection.
    /// </summary>
    public JwtService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    /// <summary>
    /// Tạo JWT token từ thông tin người dùng.
    /// </summary>
    public string GenerateToken(User user)
    {
        // Tạo danh sách thông tin được lưu bên trong JWT.
        var claims = new List<Claim>
        {
            // ID người dùng.
            new Claim(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()
            ),

            // Họ tên người dùng.
            new Claim(
                ClaimTypes.Name,
                user.FullName
            ),

            // Email người dùng.
            new Claim(
                ClaimTypes.Email,
                user.Email
            ),

            // Vai trò người dùng.
            new Claim(
                ClaimTypes.Role,
                user.Role
            )
        };

        // Chuyển SecretKey thành mảng byte.
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)
        );

        // Tạo thông tin ký token bằng HMAC-SHA256.
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        // Tạo JWT token.
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpirationMinutes
            ),
            signingCredentials: credentials
        );

        // Chuyển token thành chuỗi để trả về cho frontend.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}