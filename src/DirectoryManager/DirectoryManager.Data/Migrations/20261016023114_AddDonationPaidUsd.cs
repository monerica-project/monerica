using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationPaidUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidUsd",
                table: "VerificationRequests",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidUsd",
                table: "Submissions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidUsd",
                table: "ProblemReports",
                type: "numeric(18,2)",
                nullable: true);

            // USD-denominated donations already recorded: their USD value is the amount itself.
            // (XMR top-ups need the XMR price on the day and are backfilled separately.)
            foreach (var table in new[] { "VerificationRequests", "Submissions", "ProblemReports" })
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"PaidUsd\" = ROUND(\"PaidAmount\", 2) " +
                    "WHERE \"PaidUtc\" IS NOT NULL AND \"PaidAmount\" > 0 AND UPPER(\"PaidCurrency\") = 'USD' AND \"PaidUsd\" IS NULL;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidUsd",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "PaidUsd",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PaidUsd",
                table: "ProblemReports");
        }
    }
}
