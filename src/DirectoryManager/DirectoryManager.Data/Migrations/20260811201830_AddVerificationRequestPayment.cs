using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationRequestPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BtcPayInvoiceId",
                table: "VerificationRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "VerificationRequests",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidCurrency",
                table: "VerificationRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidUtc",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentToken",
                table: "VerificationRequests",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_PaymentToken",
                table: "VerificationRequests",
                column: "PaymentToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VerificationRequests_PaymentToken",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "BtcPayInvoiceId",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaidCurrency",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaidUtc",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaymentToken",
                table: "VerificationRequests");
        }
    }
}
