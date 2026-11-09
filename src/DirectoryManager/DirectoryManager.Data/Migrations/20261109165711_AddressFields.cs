using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <summary>
    /// Free-text Location becomes State/Region (renamed, data kept) plus new Street Address,
    /// City and Postal Code. Existing values are re-sorted into the right parts by a separate
    /// data update (with audit rows), not by this migration.
    /// </summary>
    public partial class AddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Location",
                table: "DirectoryEntries",
                newName: "StateRegion");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "DirectoryEntriesAudit",
                newName: "StateRegion");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "Submissions",
                newName: "StateRegion");

            migrationBuilder.RenameColumn(
                name: "ProposedLocation",
                table: "Submissions",
                newName: "ProposedStateRegion");

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "DirectoryEntries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "DirectoryEntries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "DirectoryEntries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "DirectoryEntriesAudit",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "DirectoryEntriesAudit",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "DirectoryEntriesAudit",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Submissions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedStreetAddress",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedCity",
                table: "Submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedPostalCode",
                table: "Submissions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedPostalCode",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedCity",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProposedStreetAddress",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "City",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "DirectoryEntriesAudit");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "City",
                table: "DirectoryEntries");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "DirectoryEntries");

            migrationBuilder.RenameColumn(
                name: "ProposedStateRegion",
                table: "Submissions",
                newName: "ProposedLocation");

            migrationBuilder.RenameColumn(
                name: "StateRegion",
                table: "Submissions",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "StateRegion",
                table: "DirectoryEntriesAudit",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "StateRegion",
                table: "DirectoryEntries",
                newName: "Location");
        }
    }
}
