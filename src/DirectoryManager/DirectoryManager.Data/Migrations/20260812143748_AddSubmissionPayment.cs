using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BtcPayInvoiceId",
                table: "Submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Submissions",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidCurrency",
                table: "Submissions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidUtc",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentToken",
                table: "Submissions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_PaymentToken",
                table: "Submissions",
                column: "PaymentToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_PaymentToken",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "BtcPayInvoiceId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaidCurrency",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaidUtc",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaymentToken",
                table: "Submissions");
        }
    }
}
