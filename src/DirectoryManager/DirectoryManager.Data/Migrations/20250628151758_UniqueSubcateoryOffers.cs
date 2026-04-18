using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSubcateoryOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days_CategoryId_SubcategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_NoSubcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId" },
                unique: true,
                filter: "[SubcategoryId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_Subcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId", "SubcategoryId" },
                unique: true,
                filter: "[SubcategoryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Offer_Type_Days_Cat_NoSubcat",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "UX_Offer_Type_Days_Cat_Subcat",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days_CategoryId_SubcategoryId",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId", "SubcategoryId" },
                unique: true,
                filter: "[CategoryId] IS NOT NULL AND [SubcategoryId] IS NOT NULL");
        }
    }
}
