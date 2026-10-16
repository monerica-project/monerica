using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAffiliateProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateCommissions");

            migrationBuilder.DropTable(
                name: "AffiliateAccounts");

            migrationBuilder.DropColumn(
                name: "ReferralCodeUsed",
                table: "SponsoredListingInvoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCodeUsed",
                table: "SponsoredListingInvoices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AffiliateAccounts",
                columns: table => new
                {
                    AffiliateAccountId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PayoutCurrency = table.Column<int>(type: "integer", nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WalletAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateAccounts", x => x.AffiliateAccountId);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissions",
                columns: table => new
                {
                    AffiliateCommissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AffiliateAccountId = table.Column<int>(type: "integer", nullable: false),
                    SponsoredListingInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayoutCurrency = table.Column<int>(type: "integer", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    PayoutTransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                        name: "FK_AffiliateCommissions_SponsoredListingInvoices_SponsoredList~",
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
    }
}
