using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CẤU HÌNH CORS
// ============================================================
//
// Cho phép Frontend React chạy tại localhost:5173
// gọi API của ASP.NET Core.
// ============================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ============================================================
// ĐĂNG KÝ CONTROLLER
// ============================================================
// Cho phép ASP.NET Core phát hiện và xử lý các API Controller.
builder.Services.AddControllers();

// ============================================================
// ĐĂNG KÝ ENTITY FRAMEWORK CORE
// ============================================================
// Sử dụng SQL Server làm cơ sở dữ liệu.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ============================================================
// CẤU HÌNH JWT SETTINGS
// ============================================================
// Đọc phần "JwtSettings" trong appsettings.json
// và đưa vào đối tượng JwtSettings.
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings")
);

// ============================================================
// ĐĂNG KÝ JWT SERVICE
// ============================================================
// Cho phép AuthController sử dụng JwtService thông qua
// Dependency Injection.
//
// AddScoped:
// Mỗi HTTP request sẽ sử dụng một instance của JwtService.
builder.Services.AddScoped<JwtService>();

// ============================================================
// CẤU HÌNH AUTHENTICATION - JWT BEARER
// ============================================================

var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>();

// Kiểm tra cấu hình JWT có đầy đủ hay không.
if (jwtSettings == null ||
    string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
    string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
    string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    throw new InvalidOperationException(
        "Cấu hình JwtSettings trong appsettings.json không hợp lệ."
    );
}

// Chuyển SecretKey thành khóa dùng để kiểm tra chữ ký JWT.
var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services
    .AddAuthentication(options =>
    {
        // Xác định JWT Bearer là phương thức xác thực mặc định.
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        // Xác định JWT Bearer là phương thức challenge mặc định.
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                // Kiểm tra JWT có đúng Issuer hay không.
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                // Kiểm tra JWT có đúng Audience hay không.
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                // Kiểm tra thời hạn token.
                ValidateLifetime = true,

                // Kiểm tra chữ ký token.
                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                // Không cho phép token hết hạn vượt quá thời gian.
                ClockSkew = TimeSpan.Zero
            };
    });

// ============================================================
// CẤU HÌNH AUTHORIZATION
// ============================================================
// Cho phép sử dụng [Authorize] trên Controller/API.
builder.Services.AddAuthorization();

// ============================================================
// CẤU HÌNH OPENAPI
// ============================================================
builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================================
// OPENAPI
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ============================================================
// HTTPS
// ============================================================
// Giữ cấu hình hiện tại cho môi trường phát triển.
app.UseHttpsRedirection();

// ============================================================
// CORS
// ============================================================
//
// Phải đặt trước Authentication/Authorization
// để request từ React được phép truy cập API.
// ============================================================

app.UseCors("FrontendPolicy");

app.UseAuthentication();

// ============================================================
// AUTHORIZATION
// ============================================================
app.UseAuthorization();

// ============================================================
// MAP CONTROLLERS
// ============================================================
app.MapControllers();

// ============================================================
// CHẠY ỨNG DỤNG
// ============================================================
app.Run();