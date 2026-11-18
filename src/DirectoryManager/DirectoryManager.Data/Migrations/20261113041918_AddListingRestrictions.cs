using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtherRestrictions",
                table: "Submissions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedCountries",
                table: "Submissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TorPolicy",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VpnPolicy",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OtherRestrictions",
                table: "DirectoryEntriesAudit",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedCountries",
                table: "DirectoryEntriesAudit",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TorPolicy",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VpnPolicy",
                table: "DirectoryEntriesAudit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OtherRestrictions",
                table: "DirectoryEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedCountries",
                table: "DirectoryEntries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TorPolicy",
                table: "DirectoryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VpnPolicy",
                table: "DirectoryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherRestrictions",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "RestrictedCountries",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "TorPolicy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "VpnPolicy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "OtherRestrictions",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "RestrictedCountries",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "TorPolicy",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "VpnPolicy",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "OtherRestrictions",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "RestrictedCountries",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "TorPolicy",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "VpnPolicy",
                table: "DirectoryEntries");
        }
    }
}
