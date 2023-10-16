using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexesOnSponsoredListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_CreateDate_UpdateDate",
                table: "SponsoredListings",
                columns: new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingInvoices_InvoiceId",
                table: "SponsoredListingInvoices",
                column: "InvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SponsoredListings_CreateDate_UpdateDate",
                table: "SponsoredListings");

            migrationBuilder.DropIndex(
                name: "IX_SponsoredListingInvoices_InvoiceId",
                table: "SponsoredListingInvoices");
        }
    }
}
