using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubCategoryPageDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PageDetails",
                table: "SubCategories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageDetails",
                table: "SubCategories");
        }
    }
}
