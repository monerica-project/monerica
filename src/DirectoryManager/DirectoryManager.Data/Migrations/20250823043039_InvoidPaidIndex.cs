using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvoidPaidIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingInvoices_DirectoryEntryId",
                table: "SponsoredListingInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Dir_PaidStatus",
                table: "SponsoredListingInvoices",
                columns: new[] { "DirectoryEntryId", "PaymentStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoice_Dir_PaidStatus",
                table: "SponsoredListingInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingInvoices_DirectoryEntryId",
                table: "SponsoredListingInvoices",
                column: "DirectoryEntryId");
        }
    }
}
