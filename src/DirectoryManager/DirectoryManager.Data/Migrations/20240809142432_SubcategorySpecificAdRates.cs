using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubcategorySpecificAdRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubcategoryId",
                table: "SponsoredListingOffers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SubcategoryId", "SponsorshipType", "Days" },
                unique: true,
                filter: "[SubcategoryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SponsoredListingOffers_SubCategories_SubcategoryId",
                table: "SponsoredListingOffers",
                column: "SubcategoryId",
                principalTable: "SubCategories",
                principalColumn: "SubCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SponsoredListingOffers_SubCategories_SubcategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId_SponsorshipType_Days",
                table: "SponsoredListingOffers");

            migrationBuilder.DropColumn(
                name: "SubcategoryId",
                table: "SponsoredListingOffers");
        }
    }
}
