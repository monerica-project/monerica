using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contact",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "DirectoryEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "Submissions",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "DirectoryEntries",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);
        }
    }
}
