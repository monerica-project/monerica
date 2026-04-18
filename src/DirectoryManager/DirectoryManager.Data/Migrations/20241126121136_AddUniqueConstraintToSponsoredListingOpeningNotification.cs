using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToSponsoredListingOpeningNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdSpotNotificationSubscriptions");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "EmailCampaignSubscriptions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateDate",
                table: "EmailCampaignSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SponsoredListingOpeningNotifications",
                columns: table => new
                {
                    SponsoredListingOpeningNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SponsorshipType = table.Column<int>(type: "int", nullable: false),
                    SubCategoryId = table.Column<int>(type: "int", nullable: true),
                    IsReminderSent = table.Column<bool>(type: "bit", nullable: false),
                    SubscribedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingOpeningNotifications", x => x.SponsoredListingOpeningNotificationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "SubCategoryId", "SubscribedDate" },
                unique: true,
                filter: "[SubCategoryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "EmailCampaignSubscriptions");

            migrationBuilder.DropColumn(
                name: "UpdateDate",
                table: "EmailCampaignSubscriptions");

            migrationBuilder.CreateTable(
                name: "AdSpotNotificationSubscriptions",
                columns: table => new
                {
                    AdSpotNotificationSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    NotifyOnExpiry = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnOpening = table.Column<bool>(type: "bit", nullable: false),
                    PreferredSponsorshipType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdSpotNotificationSubscriptions", x => x.AdSpotNotificationSubscriptionId);
                    table.ForeignKey(
                        name: "FK_AdSpotNotificationSubscriptions_EmailSubscriptions_EmailSubscriptionId",
                        column: x => x.EmailSubscriptionId,
                        principalTable: "EmailSubscriptions",
                        principalColumn: "EmailSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdSpotNotificationSubscriptions_EmailSubscriptionId",
                table: "AdSpotNotificationSubscriptions",
                column: "EmailSubscriptionId");
        }
    }
}
