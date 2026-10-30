using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAssistant.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAssistantAndCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    EmailCampaignId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.EmailCampaignId);
                    table.ForeignKey(
                        name: "FK_EmailCampaigns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailSummaryCaches",
                columns: table => new
                {
                    EmailSummaryCacheId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GmailMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSummaryCaches", x => x.EmailSummaryCacheId);
                    table.ForeignKey(
                        name: "FK_EmailSummaryCaches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignRecipients",
                columns: table => new
                {
                    EmailCampaignRecipientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailCampaignId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignRecipients", x => x.EmailCampaignRecipientId);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_EmailCampaigns_EmailCampaignId",
                        column: x => x.EmailCampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "EmailCampaignId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_EmailCampaignId_Status",
                table: "EmailCampaignRecipients",
                columns: new[] { "EmailCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_Status",
                table: "EmailCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_UserId_CreatedAtUtc",
                table: "EmailCampaigns",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSummaryCaches_UserId_GmailMessageId",
                table: "EmailSummaryCaches",
                columns: new[] { "UserId", "GmailMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailCampaignRecipients");

            migrationBuilder.DropTable(
                name: "EmailSummaryCaches");

            migrationBuilder.DropTable(
                name: "EmailCampaigns");
        }
    }
}
