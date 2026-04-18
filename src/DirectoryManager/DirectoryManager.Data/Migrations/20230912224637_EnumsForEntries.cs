using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnumsForEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DirectoryStatus",
                table: "Submissions",
                newName: "SubmissionStatus");

            migrationBuilder.AddColumn<int>(
                name: "DirectoryStatus",
                table: "DirectoryEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectoryStatus",
                table: "DirectoryEntries");

            migrationBuilder.RenameColumn(
                name: "SubmissionStatus",
                table: "Submissions",
                newName: "DirectoryStatus");
        }
    }
}
