using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailCampaignKey : Migration
    {
        /// <inheritdoc />
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the EmailCampaignKey column with a default value
            migrationBuilder.AddColumn<string>(
                name: "EmailCampaignKey",
                table: "EmailCampaigns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);

            // Update existing records to have a unique EmailCampaignKey
            migrationBuilder.Sql(@"
                UPDATE EmailCampaigns
                SET EmailCampaignKey = NEWID()
                WHERE EmailCampaignKey IS NULL OR EmailCampaignKey = ''");

            // Add a unique index for the EmailCampaignKey column
            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_EmailCampaignKey",
                table: "EmailCampaigns",
                column: "EmailCampaignKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index
            migrationBuilder.DropIndex(
                name: "IX_EmailCampaigns_EmailCampaignKey",
                table: "EmailCampaigns");

            // Remove the EmailCampaignKey column
            migrationBuilder.DropColumn(
                name: "EmailCampaignKey",
                table: "EmailCampaigns");
        }
    }
}