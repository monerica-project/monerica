using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteJavaScript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProposedSiteJavaScript",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiteJavaScript",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SiteJavaScript",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SiteJavaScript",
                table: "DirectoryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedSiteJavaScript",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SiteJavaScript",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SiteJavaScript",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "SiteJavaScript",
                table: "DirectoryEntries");
        }
    }
}
