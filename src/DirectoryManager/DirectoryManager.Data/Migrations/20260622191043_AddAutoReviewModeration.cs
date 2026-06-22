using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxUsd",
                table: "ReviewTags",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinUsd",
                table: "ReviewTags",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoModeratedAtUtc",
                table: "DirectoryEntryReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoModerationAttemptCount",
                table: "DirectoryEntryReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AutoModerationReason",
                table: "DirectoryEntryReviews",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoModerationResult",
                table: "DirectoryEntryReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAutoModerationAttemptUtc",
                table: "DirectoryEntryReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VerifiedOrderUsdValue",
                table: "DirectoryEntryReviews",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxUsd",
                table: "ReviewTags");

            migrationBuilder.DropColumn(
                name: "MinUsd",
                table: "ReviewTags");

            migrationBuilder.DropColumn(
                name: "AutoModeratedAtUtc",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AutoModerationAttemptCount",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AutoModerationReason",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "AutoModerationResult",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "LastAutoModerationAttemptUtc",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "VerifiedOrderUsdValue",
                table: "DirectoryEntryReviews");
        }
    }
}
