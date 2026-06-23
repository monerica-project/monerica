using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSocialMessenger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Submissions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Messenger",
                table: "Submissions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Social",
                table: "Submissions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Messenger",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Social",
                table: "DirectoryEntriesAudit",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "DirectoryEntries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Messenger",
                table: "DirectoryEntries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Social",
                table: "DirectoryEntries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Messenger",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Social",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "Messenger",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "Social",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "Messenger",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "Social",
                table: "DirectoryEntries");
        }
    }
}
