using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreCurrencyPaidIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SponsoredListingInvoices",
                type: "decimal(14,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "SponsoredListingInvoices",
                type: "decimal(14,12)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaidInCurrency",
                table: "SponsoredListingInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "SponsoredListingInvoices");

            migrationBuilder.DropColumn(
                name: "PaidInCurrency",
                table: "SponsoredListingInvoices");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SponsoredListingInvoices",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,12)");
        }
    }
}
