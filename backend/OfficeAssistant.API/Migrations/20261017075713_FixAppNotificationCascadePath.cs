using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class FixAppNotificationCascadePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_Users_UserId",
                table: "AppNotifications");

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_Users_UserId",
                table: "AppNotifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_Users_UserId",
                table: "AppNotifications");

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_Users_UserId",
                table: "AppNotifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.NoAction);
        }
    }
}
