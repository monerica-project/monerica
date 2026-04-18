using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_End_Start",
                table: "SponsoredListings",
                columns: new[] { "CampaignEndDate", "CampaignStartDate" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "DirectoryEntryId", "SponsorshipType" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_Type_End_Start",
                table: "SponsoredListings",
                columns: new[] { "SponsorshipType", "CampaignEndDate", "CampaignStartDate" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Queue",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "SubscribedDate", "SponsoredListingOpeningNotificationId" },
                filter: "[IsActive] = 1 AND [IsReminderSent] = 0")
                .Annotation("SqlServer:Include", new[] { "Email", "SponsorshipType", "TypeId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotifications_Email_SponsorshipType_TypeId_DirectoryEntryId",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "TypeId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotifications_SponsorshipType_TypeId_IsActive_IsReminderSent_SubscribedDate",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "SponsorshipType", "TypeId", "IsActive", "IsReminderSent", "SubscribedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "[SubcategoryId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListings_End_Start",
                table: "SponsoredListings");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListings_Type_End_Start",
                table: "SponsoredListings");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOpeningNotification_Queue",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOpeningNotifications_Email_SponsorshipType_TypeId_DirectoryEntryId",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOpeningNotifications_SponsorshipType_TypeId_IsActive_IsReminderSent_SubscribedDate",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "SubcategoryId IS NULL");
        }
    }
}
