using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReviewEmailNotificationsEnabled",
                table: "DirectoryEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewEmailNotificationsEnabledUtc",
                table: "DirectoryEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewNotifications",
                columns: table => new
                {
                    ReviewNotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    DirectoryEntryReviewId = table.Column<int>(type: "integer", nullable: true),
                    DirectoryEntryReviewCommentId = table.Column<int>(type: "integer", nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewNotifications", x => x.ReviewNotificationId);
                    table.ForeignKey(
                        name: "FK_ReviewNotifications_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNotifications_DirectoryEntryId",
                table: "ReviewNotifications",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNotifications_Status_Id",
                table: "ReviewNotifications",
                columns: new[] { "Status", "ReviewNotificationId" });

            migrationBuilder.CreateIndex(
                name: "UX_ReviewNotifications_CommentId",
                table: "ReviewNotifications",
                column: "DirectoryEntryReviewCommentId",
                unique: true,
                filter: "\"DirectoryEntryReviewCommentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ReviewNotifications_ReviewId",
                table: "ReviewNotifications",
                column: "DirectoryEntryReviewId",
                unique: true,
                filter: "\"DirectoryEntryReviewId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewNotifications");

            migrationBuilder.DropColumn(
                name: "ReviewEmailNotificationsEnabled",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "ReviewEmailNotificationsEnabledUtc",
                table: "DirectoryEntries");
        }
    }
}
