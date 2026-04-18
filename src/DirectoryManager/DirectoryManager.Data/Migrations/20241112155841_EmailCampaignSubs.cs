using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailCampaignSubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "EmailCampaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmailCampaignSubscriptions",
                columns: table => new
                {
                    EmailCampaignSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailCampaignId = table.Column<int>(type: "int", nullable: false),
                    EmailSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    SubscribedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignSubscriptions", x => x.EmailCampaignSubscriptionId);
                    table.ForeignKey(
                        name: "FK_EmailCampaignSubscriptions_EmailCampaigns_EmailCampaignId",
                        column: x => x.EmailCampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "EmailCampaignId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCampaignSubscriptions_EmailSubscriptions_EmailSubscriptionId",
                        column: x => x.EmailSubscriptionId,
                        principalTable: "EmailSubscriptions",
                        principalColumn: "EmailSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId",
                table: "EmailCampaignSubscriptions",
                column: "EmailCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailSubscriptionId",
                table: "EmailCampaignSubscriptions",
                column: "EmailSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailCampaignSubscriptions");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "EmailCampaigns");
        }
    }
}
