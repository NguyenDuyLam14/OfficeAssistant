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
    }
}