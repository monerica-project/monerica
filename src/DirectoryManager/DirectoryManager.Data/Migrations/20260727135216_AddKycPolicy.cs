using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKycPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KycPolicy",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycPolicy",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycPolicy",
                table: "DirectoryEntries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KycPolicy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "KycPolicy",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "KycPolicy",
                table: "DirectoryEntries");
        }
    }
}
