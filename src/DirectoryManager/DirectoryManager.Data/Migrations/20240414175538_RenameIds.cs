using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Submissions",
                newName: "SubmissionId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "SubCategories",
                newName: "SubCategoryId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "LogEntries",
                newName: "LogEntryId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DirectoryEntriesAudit",
                newName: "DirectoryEntriesAuditId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "DirectoryEntries",
                newName: "DirectoryEntryId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Categories",
                newName: "CategoryId");

            migrationBuilder.AddColumn<int>(
                name: "SponsorshipType",
                table: "SponsoredListings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SponsorshipType",
                table: "SponsoredListingOffers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SponsorshipType",
                table: "SponsoredListingInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SponsorshipType",
                table: "SponsoredListings");

            migrationBuilder.DropColumn(
                name: "SponsorshipType",
                table: "SponsoredListingOffers");

            migrationBuilder.DropColumn(
                name: "SponsorshipType",
                table: "SponsoredListingInvoices");

            migrationBuilder.RenameColumn(
                name: "SubmissionId",
                table: "Submissions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "SubCategoryId",
                table: "SubCategories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "LogEntryId",
                table: "LogEntries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "DirectoryEntriesAuditId",
                table: "DirectoryEntriesAudit",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "DirectoryEntryId",
                table: "DirectoryEntries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Categories",
                newName: "Id");
        }
    }
}
