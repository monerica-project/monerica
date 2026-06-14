using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "DirectoryEntryReviews",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOfficial",
                table: "DirectoryEntryReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TestedAt",
                table: "DirectoryEntryReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Entry_Official_Mod",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "IsOfficial", "ModerationStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_Entry_Official_Mod",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "IsOfficial",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "TestedAt",
                table: "DirectoryEntryReviews");
        }
    }
}
