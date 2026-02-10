using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSendDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentDateUtc",
                table: "SponsoredListingOpeningNotifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderSentLink",
                table: "SponsoredListingOpeningNotifications",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentDateUtc",
                table: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropColumn(
                name: "ReminderSentLink",
                table: "SponsoredListingOpeningNotifications");
        }
    }
}
