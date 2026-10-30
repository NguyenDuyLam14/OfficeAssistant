using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using OfficeAssistant.API.Data;
using OfficeAssistant.API.Services;
using OfficeAssistant.API.Services.AI;
using OfficeAssistant.API.Services.Word;

using OfficeAssistant.API.Services.Email;

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
builder.Services.AddControllers();

// ============================================================
// ĐĂNG KÝ ENTITY FRAMEWORK CORE
// ============================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ============================================================
// CẤU HÌNH JWT SETTINGS
// ============================================================
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings")
);

// ============================================================
// ĐĂNG KÝ JWT SERVICE
// ============================================================
builder.Services.AddScoped<JwtService>();

// ============================================================
// ĐĂNG KÝ AI SERVICE (Gemini)
// ============================================================
//
// GeminiRateLimiter là SINGLETON để mọi lời gọi Gemini
// (background scanner + người dùng soạn văn bản)
// dùng chung một bộ đếm tốc độ và quota.
// ============================================================

builder.Services.AddHttpClient();

builder.Services.AddSingleton<GeminiRateLimiter>();

builder.Services.AddHttpClient<GeminiService>();

builder.Services.AddScoped<IAIService>(
    serviceProvider =>
        serviceProvider.GetRequiredService<GeminiService>());

// Service xử lý file Word bằng OpenXML.
builder.Services.AddScoped<WordTemplateService>();

builder.Services.AddSingleton<GmailTokenProtector>();

builder.Services.AddScoped<GmailOAuthService>();

builder.Services.AddSingleton<GmailOAuthStateStore>();

// Đăng ký dịch vụ đọc email Gmail.
builder.Services.AddScoped<GmailMessageReader>();

// Đăng ký bộ quét email để phát hiện công việc.
builder.Services.AddScoped<GmailWorkEventScanner>();

// Tự động quét Gmail và phát hiện công việc trong nền.
builder.Services.AddHostedService<GmailWorkEventBackgroundService>();

builder.Services.AddScoped<GmailSender>();

builder.Services.AddHostedService<EmailCampaignBackgroundService>();

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
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                ClockSkew = TimeSpan.Zero
            };
    });

// ============================================================
// CẤU HÌNH AUTHORIZATION
// ============================================================
builder.Services.AddAuthorization();

// ============================================================
// CẤU HÌNH OPENAPI
// ============================================================
builder.Services.AddOpenApi();

// Mã hóa và giải mã token Google trước khi lưu/đọc từ SQL Server.
builder.Services.AddDataProtection();

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
app.UseHttpsRedirection();

// ============================================================
// CORS
// ============================================================
//
// Phải đặt trước Authentication/Authorization.
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