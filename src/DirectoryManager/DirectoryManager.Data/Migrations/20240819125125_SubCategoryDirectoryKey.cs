using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubCategoryDirectoryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_SubCategoryId",
                table: "DirectoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_SubCategoryId_DirectoryEntryKey",
                table: "DirectoryEntries",
                columns: new[] { "SubCategoryId", "DirectoryEntryKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_SubCategoryId_DirectoryEntryKey",
                table: "DirectoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_SubCategoryId",
                table: "DirectoryEntries",
                column: "SubCategoryId");
        }
    }
}
