using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceCodeLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceCodeLink",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCodeLink",
                table: "DirectoryEntriesAudit",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCodeLink",
                table: "DirectoryEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceCodeLink",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SourceCodeLink",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "SourceCodeLink",
                table: "DirectoryEntries");
        }
    }
}
