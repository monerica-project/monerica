using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewAiOrderCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiOrderCheckAttempts",
                table: "DirectoryEntryReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiOrderCheckDetails",
                table: "DirectoryEntryReviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiOrderCheckModel",
                table: "DirectoryEntryReviews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiOrderCheckQueued",
                table: "DirectoryEntryReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AiOrderCheckResult",
                table: "DirectoryEntryReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiOrderCheckSummary",
                table: "DirectoryEntryReviews",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiOrderCheckedUtc",
                table: "DirectoryEntryReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiOrderStatus",
                table: "DirectoryEntryReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AiOrderUsdValue",
                table: "DirectoryEntryReviews",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSuggestedTags",
                table: "DirectoryEntryReviews",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectoryEntryReviewId",
                table: "AiCallLogs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiOrderCheckAttempts",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckDetails",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckModel",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckQueued",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckResult",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckSummary",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderCheckedUtc",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderStatus",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiOrderUsdValue",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AiSuggestedTags",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "DirectoryEntryReviewId",
                table: "AiCallLogs");
        }
    }
}
