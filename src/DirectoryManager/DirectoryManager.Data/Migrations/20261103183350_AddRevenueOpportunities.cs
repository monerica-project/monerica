using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "SponsoredListingInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ListingPageViews",
                columns: table => new
                {
                    DirectoryEntryKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Views30 = table.Column<int>(type: "integer", nullable: false),
                    Views90 = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPageViews", x => x.DirectoryEntryKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingPageViews");

            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "SponsoredListingInvoices");
        }
    }
}
