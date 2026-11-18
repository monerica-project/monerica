using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSwapFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Confirmations",
                table: "DirectoryEntryReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMinutes",
                table: "DirectoryEntryReviews",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmations",
                table: "DirectoryEntryReviews");

            migrationBuilder.DropColumn(
                name: "ProcessingMinutes",
                table: "DirectoryEntryReviews");
        }
    }
}
