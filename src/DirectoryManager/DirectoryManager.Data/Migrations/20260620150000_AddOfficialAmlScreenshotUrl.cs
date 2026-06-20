using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialAmlScreenshotUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmlScreenshotUrl",
                table: "DirectoryEntryReviews",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmlScreenshotUrl",
                table: "DirectoryEntryReviews");
        }
    }
}
