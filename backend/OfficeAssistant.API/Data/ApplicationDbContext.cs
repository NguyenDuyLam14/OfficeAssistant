using Microsoft.EntityFrameworkCore;
using OfficeAssistant.API.Models;

namespace OfficeAssistant.API.Data;

/// <summary>
/// DbContext chính của ứng dụng.
/// Quản lý kết nối và thao tác dữ liệu với SQL Server.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Bảng người dùng.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Bảng mẫu văn bản.
    /// </summary>
    public DbSet<DocumentTemplate> DocumentTemplates =>
        Set<DocumentTemplate>();

    /// <summary>
    /// Bảng văn bản.
    /// </summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>
    /// Các kết nối Gmail của người dùng.
    /// </summary>
    public DbSet<GmailConnection> GmailConnections =>
        Set<GmailConnection>();

    /// <summary>
    /// Các lịch công việc được AI phát hiện từ email.
    /// </summary>
    public DbSet<DetectedWorkEvent> DetectedWorkEvents =>
        Set<DetectedWorkEvent>();

    /// <summary>
    /// Các thông báo trong ứng dụng.
    /// </summary>
    public DbSet<AppNotification> AppNotifications =>
        Set<AppNotification>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình bảng Users.
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Role)
                .HasMaxLength(50)
                .IsRequired();
        });

        // Cấu hình bảng DocumentTemplates.
        modelBuilder.Entity<DocumentTemplate>(entity =>
        {
            entity.HasKey(x => x.DocumentTemplateId);

            entity.Property(x => x.TemplateName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.DocumentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.FilePath)
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasMaxLength(1000);
        });

        // Cấu hình bảng Documents.
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(x => x.DocumentId);

            entity.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.DocumentType)
                .HasMaxLength(100)
                .IsRequired();

            // Quan hệ Document -> User.
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quan hệ Document -> DocumentTemplate.
            entity.HasOne(x => x.DocumentTemplate)
                .WithMany()
                .HasForeignKey(x => x.DocumentTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Cấu hình bảng GmailConnections.
        modelBuilder.Entity<GmailConnection>(entity =>
        {
            entity.HasKey(x => x.GmailConnectionId);

            entity.Property(x => x.GmailAddress)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.AccessToken)
                .HasMaxLength(4000)
                .IsRequired();

            entity.Property(x => x.RefreshToken)
                .HasMaxLength(4000);

            // Mỗi người dùng chỉ có một kết nối Gmail trong phiên bản đầu.
            entity.HasIndex(x => x.UserId)
                .IsUnique();

            // Mỗi địa chỉ Gmail chỉ được liên kết với một tài khoản.
            entity.HasIndex(x => x.GmailAddress)
                .IsUnique();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // Cấu hình bảng DetectedWorkEvents.
        modelBuilder.Entity<DetectedWorkEvent>(entity =>
        {
            entity.HasKey(x => x.DetectedWorkEventId);

            entity.Property(x => x.GmailMessageId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.EventType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.TimeZoneId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired();

            // Một email chỉ tạo tối đa một đề xuất lịch
            // cho cùng một người dùng trong phiên bản đầu.
            entity.HasIndex(x => new
            {
                x.UserId,
                x.GmailMessageId
            }).IsUnique();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng AppNotifications.
        modelBuilder.Entity<AppNotification>(entity =>
        {
            entity.HasKey(x => x.AppNotificationId);

            entity.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.NotificationType)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.UserId,
                x.IsRead,
                x.CreatedAtUtc
            });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DetectedWorkEvent)
                .WithMany()
                .HasForeignKey(x => x.DetectedWorkEventId)
                .OnDelete(DeleteBehavior.SetNull);
        });

    }
}