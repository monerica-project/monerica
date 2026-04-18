using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectoryEntryStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_DirectoryEntryId_DirectoryStatus",
                table: "DirectoryEntries",
                columns: new[] { "DirectoryEntryId", "DirectoryStatus" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectoryEntries_DirectoryEntryId_DirectoryStatus",
                table: "DirectoryEntries");
        }
    }
}
