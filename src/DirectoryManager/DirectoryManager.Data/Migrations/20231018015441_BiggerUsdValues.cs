using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class BiggerUsdValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "SponsoredListingInvoices",
                type: "decimal(20,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,12)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SponsoredListingInvoices",
                type: "decimal(20,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,12)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "SponsoredListingInvoices",
                type: "decimal(14,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,12)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SponsoredListingInvoices",
                type: "decimal(14,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,12)");
        }
    }
}
