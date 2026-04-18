using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailMessageIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId",
                table: "SentEmailRecords");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId_EmailMessageId",
                table: "SentEmailRecords",
                columns: new[] { "EmailSubscriptionId", "EmailMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSubscriptions_IsSubscribed",
                table: "EmailSubscriptions",
                column: "IsSubscribed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId_EmailMessageId",
                table: "SentEmailRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmailSubscriptions_IsSubscribed",
                table: "EmailSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId",
                table: "SentEmailRecords",
                column: "EmailSubscriptionId");
        }
    }
}
