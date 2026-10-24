using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeaturedDirectoryEntryId",
                table: "Subcategories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeaturedDirectoryEntryId",
                table: "Categories",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturedDirectoryEntryId",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "FeaturedDirectoryEntryId",
                table: "Categories");
        }
    }
}
