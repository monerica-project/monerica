using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditMainProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProofLink",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoLink",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FoundedDate",
                table: "DirectoryEntriesAudit",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofLink",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "VideoLink",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "FoundedDate",
                table: "DirectoryEntriesAudit");
        }
    }
}
