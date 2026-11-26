using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations;

/// <inheritdoc />
public partial class AffiliateCommissionAmountPrecision : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "PaymentCurrencyAmount",
            table: "AffiliateCommissionsEarned",
            type: "numeric(28,18)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,8)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "PaymentCurrencyAmount",
            table: "AffiliateCommissionsEarned",
            type: "numeric(18,8)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(28,18)");
    }
}
