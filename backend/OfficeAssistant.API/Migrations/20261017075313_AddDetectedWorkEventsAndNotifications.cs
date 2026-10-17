using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectedWorkEventsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DetectedWorkEvents",
                columns: table => new
                {
                    DetectedWorkEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GmailMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectedWorkEvents", x => x.DetectedWorkEventId);
                    table.ForeignKey(
                        name: "FK_DetectedWorkEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AppNotifications",
                columns: table => new
                {
                    AppNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DetectedWorkEventId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotifications", x => x.AppNotificationId);
                    table.ForeignKey(
                        name: "FK_AppNotifications_DetectedWorkEvents_DetectedWorkEventId",
                        column: x => x.DetectedWorkEventId,
                        principalTable: "DetectedWorkEvents",
                        principalColumn: "DetectedWorkEventId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_DetectedWorkEventId",
                table: "AppNotifications",
                column: "DetectedWorkEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId_IsRead_CreatedAtUtc",
                table: "AppNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DetectedWorkEvents_UserId_GmailMessageId",
                table: "DetectedWorkEvents",
                columns: new[] { "UserId", "GmailMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppNotifications");

            migrationBuilder.DropTable(
                name: "DetectedWorkEvents");
        }
    }
}
