using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateLinkAlternates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Link2A",
                table: "DirectoryEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Link3A",
                table: "DirectoryEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkA",
                table: "DirectoryEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Link2A",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "Link3A",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "LinkA",
                table: "DirectoryEntries");
        }
    }
}
