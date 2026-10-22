using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedGmailMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedGmailMessages",
                columns: table => new
                {
                    ProcessedGmailMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GmailMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedGmailMessages", x => x.ProcessedGmailMessageId);
                    table.ForeignKey(
                        name: "FK_ProcessedGmailMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedGmailMessages_UserId_GmailMessageId",
                table: "ProcessedGmailMessages",
                columns: new[] { "UserId", "GmailMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedGmailMessages");
        }
    }
}
