using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexesOnTablesForEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId",
                table: "EmailCampaignSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId_IsActive",
                table: "EmailCampaignSubscriptions",
                columns: new[] { "EmailCampaignId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId_IsActive",
                table: "EmailCampaignSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId",
                table: "EmailCampaignSubscriptions",
                column: "EmailCampaignId");
        }
    }
}
