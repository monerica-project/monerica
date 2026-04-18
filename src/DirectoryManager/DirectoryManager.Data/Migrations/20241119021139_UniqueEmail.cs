using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "SponsoredListingInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

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
                name: "IX_EmailSubscriptions_Email",
                table: "EmailSubscriptions",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdSpotNotificationSubscriptions_EmailSubscriptionId",
                table: "AdSpotNotificationSubscriptions",
                column: "EmailSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdSpotNotificationSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EmailSubscriptions_Email",
                table: "EmailSubscriptions");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "SponsoredListingInvoices");
        }
    }
}
