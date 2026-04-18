using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class CategorySponsor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.RenameColumn(
                name: "SubCategoryId",
                table: "SponsoredListingOpeningNotifications",
                newName: "TypeId");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "SponsoredListingInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "TypeId", "SubscribedDate" },
                unique: true,
                filter: "[TypeId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "SponsoredListingInvoices");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "SponsoredListingOpeningNotifications",
                newName: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "SubCategoryId", "SubscribedDate" },
                unique: true,
                filter: "[SubCategoryId] IS NOT NULL");
        }
    }
}
