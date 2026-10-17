using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GmailConnections",
                columns: table => new
                {
                    GmailConnectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GmailAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmailConnections", x => x.GmailConnectionId);
                    table.ForeignKey(
                        name: "FK_GmailConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GmailConnections_GmailAddress",
                table: "GmailConnections",
                column: "GmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GmailConnections_UserId",
                table: "GmailConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GmailConnections");
        }
    }
}
