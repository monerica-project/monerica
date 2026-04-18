using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SponsoredListingInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCodeUsed",
                table: "SponsoredListingInvoices",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AffiliateAccounts",
                columns: table => new
                {
                    AffiliateAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferralCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    WalletAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayoutCurrency = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateAccounts", x => x.AffiliateAccountId);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissions",
                columns: table => new
                {
                    AffiliateCommissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsoredListingInvoiceId = table.Column<int>(type: "int", nullable: false),
                    AffiliateAccountId = table.Column<int>(type: "int", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    PayoutCurrency = table.Column<int>(type: "int", nullable: false),
                    PayoutStatus = table.Column<int>(type: "int", nullable: false),
                    PayoutTransactionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissions", x => x.AffiliateCommissionId);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_AffiliateAccounts_AffiliateAccountId",
                        column: x => x.AffiliateAccountId,
                        principalTable: "AffiliateAccounts",
                        principalColumn: "AffiliateAccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_SponsoredListingInvoices_SponsoredListingInvoiceId",
                        column: x => x.SponsoredListingInvoiceId,
                        principalTable: "SponsoredListingInvoices",
                        principalColumn: "SponsoredListingInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AffiliateAccount_ReferralCode",
                table: "AffiliateAccounts",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateAccountId",
                table: "AffiliateCommissions",
                column: "AffiliateAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_AffiliateCommission_Invoice",
                table: "AffiliateCommissions",
                column: "SponsoredListingInvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateCommissions");

            migrationBuilder.DropTable(
                name: "AffiliateAccounts");

            migrationBuilder.DropColumn(
                name: "ReferralCodeUsed",
                table: "SponsoredListingInvoices");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SponsoredListingInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
