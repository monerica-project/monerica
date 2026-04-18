using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameIds2AndSubCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "SponsoredListingOffers",
                newName: "SponsoredListingOfferId");

            migrationBuilder.AddColumn<int>(
                name: "SubCategoryId",
                table: "SponsoredListings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubCategoryId",
                table: "SponsoredListingInvoices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "SponsoredListings");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "SponsoredListingInvoices");

            migrationBuilder.RenameColumn(
                name: "SponsoredListingOfferId",
                table: "SponsoredListingOffers",
                newName: "Id");
        }
    }
}
