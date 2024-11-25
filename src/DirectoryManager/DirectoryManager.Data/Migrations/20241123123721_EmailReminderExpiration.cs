using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailReminderExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReminderSent",
                table: "SponsoredListingInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_CampaignEndDate",
                table: "SponsoredListings",
                column: "CampaignEndDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListings_CampaignEndDate",
                table: "SponsoredListings");

            migrationBuilder.DropColumn(
                name: "IsReminderSent",
                table: "SponsoredListingInvoices");
        }
    }
}
