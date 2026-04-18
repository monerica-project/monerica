using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class CategoryIdOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId_SponsorshipType_Days",
                table: "SponsoredListingOffers");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "SponsoredListingOffers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_CategoryId",
                table: "SponsoredListingOffers",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days_CategoryId_SubcategoryId",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId", "SubcategoryId" },
                unique: true,
                filter: "[CategoryId] IS NOT NULL AND [SubcategoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId",
                table: "SponsoredListingOffers",
                column: "SubcategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_SponsoredListingOffers_Categories_CategoryId",
                table: "SponsoredListingOffers",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SponsoredListingOffers_Categories_CategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_CategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days_CategoryId_SubcategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SubcategoryId", "SponsorshipType", "Days" },
                unique: true,
                filter: "[SubcategoryId] IS NOT NULL");
        }
    }
}
