using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuaranteeAmountCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename AmountUsd -> Amount (preserves the existing values), widen int -> numeric so
            // BTC/XMR can be fractional, and add Currency (existing rows were all USD).
            migrationBuilder.RenameColumn(
                name: "AmountUsd",
                table: "DirectoryEntryGuarantees",
                newName: "Amount");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "DirectoryEntryGuarantees",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "DirectoryEntryGuarantees",
                type: "integer",
                nullable: false,
                defaultValue: 2); // Currency.USD
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "DirectoryEntryGuarantees");

            migrationBuilder.AlterColumn<int>(
                name: "Amount",
                table: "DirectoryEntryGuarantees",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "DirectoryEntryGuarantees",
                newName: "AmountUsd");
        }
    }
}
