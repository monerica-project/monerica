using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExistingIdOnSubmit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DirectoryEntryId",
                table: "Submissions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_DirectoryEntryId",
                table: "Submissions",
                column: "DirectoryEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_DirectoryEntries_DirectoryEntryId",
                table: "Submissions",
                column: "DirectoryEntryId",
                principalTable: "DirectoryEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_DirectoryEntries_DirectoryEntryId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_DirectoryEntryId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DirectoryEntryId",
                table: "Submissions");
        }
    }
}
