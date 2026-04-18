using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTitleToName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "SubCategory",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Category",
                newName: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "SubCategory",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Category",
                newName: "Title");
        }
    }
}
