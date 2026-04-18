using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCategoryFromEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectoryEntries_Categories_CategoryId",
                table: "DirectoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_CategoryId",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "DirectoryEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "DirectoryEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_CategoryId",
                table: "DirectoryEntries",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectoryEntries_Categories_CategoryId",
                table: "DirectoryEntries",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }
    }
}
