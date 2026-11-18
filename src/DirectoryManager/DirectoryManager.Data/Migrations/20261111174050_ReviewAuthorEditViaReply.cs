using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAuthorEditViaReply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "RatingChangedFrom",
                table: "DirectoryEntryReviewComments",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "RatingChangedTo",
                table: "DirectoryEntryReviewComments",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedReviewTagIdsCsv",
                table: "DirectoryEntryReviewComments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedTagsAppliedUtc",
                table: "DirectoryEntryReviewComments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatingChangedFrom",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "RatingChangedTo",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "RequestedReviewTagIdsCsv",
                table: "DirectoryEntryReviewComments");

            migrationBuilder.DropColumn(
                name: "RequestedTagsAppliedUtc",
                table: "DirectoryEntryReviewComments");
        }
    }
}
