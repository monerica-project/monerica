using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubcategorySpecificAdRatesNulls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "SubcategoryId IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers");
        }
    }
}
